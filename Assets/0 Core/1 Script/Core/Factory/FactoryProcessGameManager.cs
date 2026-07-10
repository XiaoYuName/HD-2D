using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 工厂加工（传送带下压）小游戏状态机：开局按「生产量」确定本局出货总数 → 产品在传送带上匀速右移 →
/// 合格品进入下压区时下压（GOOD/OK 得分），次品需跳过，按错次品或漏掉合格品记失败 → 全部出货并离场后结算。
/// 生产量 = <see cref="FactoryGameConfig.BaseProductionVolume"/> + 设备「生产量」加成之和；
/// 良品率% = <see cref="FactoryGameConfig.BaseYieldRate"/> + 设备「良品率」加成之和（均见 <see cref="FactoryEquipManager"/>），
/// 开局按此百分比换算为 0~1 概率，逐件抽检是否合格。
/// 只负责数据与规则；产品位置每帧在 <see cref="Update"/> 推进，UI 由 <see cref="FactoryProcessPanel"/> 读取 <see cref="Items"/> 渲染。
/// 结算奖励走 <see cref="InventoryManager"/>。
/// </summary>
public class FactoryProcessGameManager : MonoBehaviour
{
    [LabelText("配置")][SerializeField] FactoryGameConfig config;

    public FactoryGameConfig Config => config;

    public enum GameState
    {
        /// <summary>准备：未开局。</summary>
        Ready,
        /// <summary>进行中：传送带运转，按生产量陆续出货。</summary>
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
    /// <summary>本局结束：积分、成功数、失败数、完成率(0~1)、奖励金币。</summary>
    public event Action<int, int, int, float, int> OnRoundEnd;
    #endregion

    #region 运行时状态
    GameState state = GameState.Ready;
    bool paused;   // 暂停（如弹出「提前结束」确认面板时）：传送带 / 出货 / 下压全部冻结
    float spawnTimer;
    int nextItemId;
    int totalToSpawn;   // 本局出货总数 = 生产量（开局按配置 + 设备加成确定）
    int totalYieldPercent;   // 本局良品率% = 基础良品率 + 设备加成（开局确定，逐件抽检按此换算为 0~1 概率）
    int score;
    int successCount;
    int failCount;
    readonly List<Item> items = new ();

    // 下压判定区（归一化）：中心与半宽由界面按凹槽 UI 实际位置/宽度经 SetPressZone 注入，逻辑不再依赖配置数值
    float pressCenter = 0.5f;
    float pressHalfWidth = 0.08f;

    public GameState State => state;
    public int Score => score;
    public int SuccessCount => successCount;
    public int FailCount => failCount;
    public IReadOnlyList<Item> Items => items;
    /// <summary>本局生产量（出货总数 = 已按配置 + 设备加成算好，供结算发放数量复用，保证与出货数一致）。</summary>
    public int ProductionVolume => totalToSpawn;
    /// <summary>本局良品率%（基础良品率 + 设备加成，已按开局时点算好，供 UI 复用）。</summary>
    public int YieldRatePercent => totalYieldPercent;

    /// <summary>完成率 = 制作成功数 / (成功数 + 失败数)；尚无成功/失败时记 1。</summary>
    public float Completion => (successCount + failCount) > 0 ? successCount / (float)(successCount + failCount) : 1f;
    #endregion
    void Update()
    {
        if(state != GameState.Playing || paused)
            return;

        StepBelt(Time.deltaTime);
        StepSpawn(Time.deltaTime);

        // 生产量已全部出货且传送带上无剩余产品 → 本局结束
        if(nextItemId >= totalToSpawn && items.Count == 0)
            EndRound();
    }

    #region 开局 / 结束
    /// <summary>是否满足开局条件（体力足够）。</summary>
    public bool CanStartRound() => true;// GameDataManager.Instance.GetProperty(PropertyType.Strength).Value >= config.StartSpCost;

    /// <summary>开始一局：清场、归零计数、按生产量确定本局出货总数。条件不足返回 false。</summary>
    public bool StartRound()
    {
        if(state == GameState.Playing || !CanStartRound())
            return false;

        // GameDataManager.Instance.RemoveProperty(PropertyType.Strength, (int)config.StartSpCost);

        items.Clear();
        nextItemId = 0;
        score = successCount = failCount = 0;
        spawnTimer = 0f;
        paused = false;

        // 本局出货总数 = 生产量：基础生产量 + 设备「生产量」加成之和
        totalToSpawn = config.BaseProductionVolume
            + FactoryEquipManager.St.SumBonus(FactoryEquipBonusType.ProductionVolume);
        // 本局良品率% = 基础良品率 + 设备「良品率」加成之和，逐件抽检按此换算为 0~1 概率
        totalYieldPercent = Mathf.Clamp(config.BaseYieldRate
            + FactoryEquipManager.St.SumBonus(FactoryEquipBonusType.Yield), 0, 100);

        SetState(GameState.Playing);
        OnScoreChanged?.Invoke();
        return true;
    }

    /// <summary>结束本局：结算奖励金币并广播结果。可由倒计时归零或玩家「结束本局」触发。</summary>
    public void EndRound()
    {
        if(state != GameState.Playing)
            return;

        int reward = successCount * config.RewardPerSuccess;
        if(reward > 0)
            GameDataManager.Instance.AddProperty(PropertyType.Coin, reward);

        SetState(GameState.Ended);
        OnRoundEnd?.Invoke(score, successCount, failCount, Completion, reward);
    }

    /// <summary>
    /// 提前结束（放弃）本局：不结算奖励、不广播 <see cref="OnRoundEnd"/>（即什么也不产出），仅停止本局。
    /// 由「结束本局」确认面板的「确认结束」触发，与倒计时归零的正常结算 <see cref="EndRound"/> 区分。
    /// </summary>
    public void AbortRound()
    {
        if(state != GameState.Playing)
            return;
        paused = false;
        items.Clear();
        SetState(GameState.Ended);
    }

    /// <summary>暂停 / 恢复本局（弹出确认面板时暂停，取消时恢复）：暂停期间传送带 / 倒计时 / 下压全部冻结。</summary>
    public void SetPaused(bool value) => paused = value;

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
        // 已按生产量出满货则不再生成，等待剩余产品离场后结束
        if(nextItemId >= totalToSpawn)
            return;

        spawnTimer -= dt;
        if(spawnTimer > 0f)
            return;
        spawnTimer = config.SpawnInterval;

        bool qualified = UnityEngine.Random.value < totalYieldPercent / 100f;
        items.Add(new Item { Id = nextItemId++, Pos = 0f, Qualified = qualified });
    }
    #endregion

    #region 下压判定
    /// <summary>由界面在布局后注入下压判定区（归一化中心与半宽，取自凹槽 UI 的实际位置 / 宽度）。</summary>
    public void SetPressZone(float centerNorm, float halfWidthNorm)
    {
        pressCenter = centerNorm;
        pressHalfWidth = halfWidthNorm;
    }

    /// <summary>下压：判定离下压区中心最近且在区内的产品。空压无惩罚，压次品判失败，压合格品按完美区给 GOOD/OK。</summary>
    public PressResult PressStamp()
    {
        if(state != GameState.Playing || paused)
            return PressResult.Empty;

        Item hit = null;
        float best = float.MaxValue;
        foreach(Item it in items)
        {
            if(it.Resolved)
                continue;
            float d = Mathf.Abs(it.Pos - pressCenter);
            if(d <= pressHalfWidth && d < best)
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
