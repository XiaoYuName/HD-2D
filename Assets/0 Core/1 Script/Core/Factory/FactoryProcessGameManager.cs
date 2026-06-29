using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 工厂加工（传送带下压）小游戏状态机：开局扣体力 → 产品在传送带上匀速右移 →
/// 合格品进入下压区时下压（GOOD/OK 得分），次品需跳过，按错次品或漏掉合格品记失败 → 倒计时结束结算。
/// 只负责数据与规则；产品位置每帧在 <see cref="Update"/> 推进，UI 由 <see cref="FactoryProcessPanel"/> 读取 <see cref="Items"/> 渲染。
/// 开局 / 再来一局消耗体力走 <see cref="PlayerStats"/>，结算奖励走 <see cref="PlayerBag.Money"/>。
/// </summary>
public class FactoryProcessGameManager : MonoBehaviour
{
    public static FactoryProcessGameManager St;

    [LabelText("配置")][SerializeField] FactoryProcessGameConfig config;

    public FactoryProcessGameConfig Config => config;

    public enum GameState
    {
        /// <summary>准备：未开局。</summary>
        Ready,
        /// <summary>进行中：传送带运转，倒计时推进。</summary>
        Playing,
        /// <summary>本局结束：等待结算 / 重开。</summary>
        Ended,
    }

    /// <summary>一次下压的判定结果。</summary>
    public enum PressResult
    {
        /// <summary>下压区内无产品，空压（无惩罚）。</summary>
        Empty,
        /// <summary>压到次品，判失败。</summary>
        Bad,
        /// <summary>压中合格品（一般）。</summary>
        Ok,
        /// <summary>压中合格品（完美区）。</summary>
        Good,
    }

    /// <summary>传送带上的一件产品（位置归一化：0=入口，1=出口）。</summary>
    public class Item
    {
        public int Id;
        public float Pos;
        public bool Qualified;
        public bool Resolved;   // 已下压或已离场判定
    }

    #region 事件
    /// <summary>状态变化。</summary>
    public event Action<GameState> OnStateChanged;
    /// <summary>积分 / 成功 / 失败 / 完成率刷新。</summary>
    public event Action OnScoreChanged;
    /// <summary>剩余时间刷新（秒）。</summary>
    public event Action<float> OnTimeChanged;
    /// <summary>本局结束：积分、成功数、失败数、完成率(0~1)、奖励金币。</summary>
    public event Action<int, int, int, float, int> OnRoundEnd;
    #endregion

    #region 运行时状态
    GameState state = GameState.Ready;
    float timeLeft;
    float spawnTimer;
    int nextItemId;
    int score;
    int successCount;
    int failCount;
    int qualifiedSpawned;
    readonly List<Item> items = new ();

    public GameState State => state;
    public float TimeLeft => timeLeft;
    public int Score => score;
    public int SuccessCount => successCount;
    public int FailCount => failCount;
    public IReadOnlyList<Item> Items => items;

    /// <summary>完成率 = 成功数 / 已出货合格品数；无合格品时记 1。</summary>
    public float Completion => qualifiedSpawned > 0 ? successCount / (float)qualifiedSpawned : 1f;
    #endregion

    void Awake() => St = this;

    void Update()
    {
        if(state != GameState.Playing)
            return;

        timeLeft -= Time.deltaTime;
        OnTimeChanged?.Invoke(Mathf.Max(0f, timeLeft));

        StepBelt(Time.deltaTime);
        StepSpawn(Time.deltaTime);

        if(timeLeft <= 0f)
            EndRound();
    }

    #region 开局 / 结束
    /// <summary>是否满足开局条件（体力足够）。</summary>
    public bool CanStartRound() => true;// PlayerInfo.St.Stats.CanConsumeSp(config.StartSpCost);

    /// <summary>开始一局：扣体力、清场、归零计数、开始倒计时。条件不足返回 false。</summary>
    public bool StartRound()
    {
        if(state == GameState.Playing || !CanStartRound())
            return false;

        // PlayerInfo.St.Stats.SubSp(config.StartSpCost);

        items.Clear();
        nextItemId = 0;
        score = successCount = failCount = qualifiedSpawned = 0;
        timeLeft = config.Duration;
        spawnTimer = 0f;

        SetState(GameState.Playing);
        OnScoreChanged?.Invoke();
        OnTimeChanged?.Invoke(timeLeft);
        return true;
    }

    /// <summary>结束本局：结算奖励金币并广播结果。可由倒计时归零或玩家「结束本局」触发。</summary>
    public void EndRound()
    {
        if(state != GameState.Playing)
            return;

        int reward = successCount * config.RewardPerSuccess;
        if(reward > 0)
            PlayerInfo.St.Bag.AddMoney(reward);

        SetState(GameState.Ended);
        OnRoundEnd?.Invoke(score, successCount, failCount, Completion, reward);
    }

    /// <summary>回到准备状态，准备下一局。</summary>
    public void ResetToReady()
    {
        if(state == GameState.Playing)
            return;
        items.Clear();
        SetState(GameState.Ready);
    }
    #endregion

    #region 传送带推进
    void StepBelt(float dt)
    {
        float move = config.BeltSpeed * dt;
        for(int i = items.Count - 1; i >= 0; i--)
        {
            Item it = items[i];
            it.Pos += move;
            if(it.Pos < 1f)
                continue;

            // 离场：未下压的合格品记漏件（失败），未下压的次品为正确跳过
            if(!it.Resolved && it.Qualified)
            {
                failCount++;
                OnScoreChanged?.Invoke();
            }
            items.RemoveAt(i);
        }
    }

    void StepSpawn(float dt)
    {
        spawnTimer -= dt;
        if(spawnTimer > 0f)
            return;
        spawnTimer = config.SpawnInterval;

        bool qualified = config.RollQualified();
        items.Add(new Item { Id = nextItemId++, Pos = 0f, Qualified = qualified });
        if(qualified)
        {
            qualifiedSpawned++;
            // 出货合格品数（完成率分母）变化时刷新战况栏，否则左侧完成率会停在上次下压时的旧值，
            // 与结算面板按结束时刻分母算出的完成率不一致。
            OnScoreChanged?.Invoke();
        }
    }
    #endregion

    #region 下压判定
    /// <summary>下压：判定离下压区中心最近且在区内的产品。空压无惩罚，压次品判失败，压合格品按完美区给 GOOD/OK。</summary>
    public PressResult PressStamp()
    {
        if(state != GameState.Playing)
            return PressResult.Empty;

        Item hit = null;
        float best = float.MaxValue;
        foreach(Item it in items)
        {
            if(it.Resolved)
                continue;
            float d = Mathf.Abs(it.Pos - config.PressCenter);
            if(d <= config.PressHalfWidth && d < best)
            {
                best = d;
                hit = it;
            }
        }

        if(hit == null)
            return PressResult.Empty;

        hit.Resolved = true;

        if(!hit.Qualified)
        {
            failCount++;
            OnScoreChanged?.Invoke();
            return PressResult.Bad;
        }

        PressResult r = best <= config.GoodHalfWidth ? PressResult.Good : PressResult.Ok;
        score += r == PressResult.Good ? config.GoodScore : config.OkScore;
        successCount++;
        OnScoreChanged?.Invoke();
        return r;
    }
    #endregion

    void SetState(GameState s)
    {
        state = s;
        OnStateChanged?.Invoke(s);
    }
}
