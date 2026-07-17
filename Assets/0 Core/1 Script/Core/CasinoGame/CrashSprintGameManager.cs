using System;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 爆点冲刺小游戏状态机：下注 → 开局预生成隐藏爆点 → 倍率匀速上涨 → 主动止盈 / 触爆失败。
/// 只负责数据与规则；倍率每帧在 <see cref="Update"/> 中推进，UI 由 <see cref="CrashSprintPanel"/> 通过事件与 getter 刷新。
/// 下注与收益均走游戏币，每局开始额外消耗体力走 <see cref="GameDataManager"/>。
/// </summary>
public class CrashSprintGameManager : MonoBehaviour
{
    public static CrashSprintGameManager St;

    [LabelText("配置")]
    [SerializeField] CrashSprintGameConfig config;
    [LabelText("进入消耗配置")]
    [SerializeField] GameEnterPanelConfig enterConfig;

    public CrashSprintGameConfig Config => config;
    public GameEnterPanelConfig EnterConfig => enterConfig;

    public enum GameState
    {
        /// <summary>下注阶段：可调下注金额，未开局。</summary>
        Betting,
        /// <summary>进行中：倍率匀速上涨，可随时止盈。</summary>
        Playing,
        /// <summary>本局结束：等待开始下一局。</summary>
        Ended,
    }

    /// <summary>开局条件校验结果。</summary>
    public enum StartCondition
    {
        /// <summary>满足开局条件。</summary>
        Ok,
        /// <summary>游戏币不足。</summary>
        NotEnoughGameCoin,
        /// <summary>体力不足。</summary>
        NotEnoughStamina,
    }

    #region 事件
    /// <summary>状态变化。</summary>
    public event Action<GameState> OnStateChanged;
    /// <summary>下注金额变化。</summary>
    public event Action<int> OnBetChanged;
    /// <summary>开局（爆点已生成但对玩家隐藏）。</summary>
    public event Action OnRoundStart;
    /// <summary>本局结束：是否止盈成功、止盈倍率、本局爆点、实际收益。</summary>
    public event Action<bool, float, float, int> OnRoundEnd;
    #endregion

    #region 运行时状态
    GameState state = GameState.Betting;
    int bet;
    float crashPoint;          // 本局爆点（全程对玩家隐藏，结算时公开）
    float currentMultiplier;   // 当前界面倍率
    float stopMultiplier;      // 止盈时锁定的倍率
    int lastPayout;            // 上局收益

    public GameState State => state;
    public int Bet => bet;
    public float CurrentMultiplier => currentMultiplier;
    public float CrashPoint => crashPoint;
    public float StopMultiplier => stopMultiplier;
    public int LastPayout => lastPayout;

    /// <summary>当前可提现收益 = 下注 × 当前倍率（向下取整）。</summary>
    public int ExpectedPayout => Mathf.FloorToInt(bet * currentMultiplier);
    #endregion

    void Awake()
    {
        St = this;
        if(config != null)
            bet = config.MinBet;
    }

    void Update()
    {
        if(state != GameState.Playing)
            return;

        // 匀速上涨，封顶倍率上限
        currentMultiplier += config.GrowthPerSecond * Time.deltaTime;

        // 触爆：界面倍率达到/越过本局隐藏爆点 → 强制失败结束
        if(currentMultiplier >= crashPoint)
        {
            currentMultiplier = Mathf.Min(crashPoint, config.MaxMultiplier);
            EndRound(false);
        }
    }

    #region 下注
    /// <summary>设置下注金额（按步进对齐并限制在区间内）。仅非进行中可调整。</summary>
    public void SetBet(int amount)
    {
        if(state == GameState.Playing)
            return;

        int step = config.BetStep;
        int aligned = Mathf.RoundToInt(amount / (float)step) * step;
        bet = Mathf.Clamp(aligned, config.MinBet, config.MaxBet);
        OnBetChanged?.Invoke(bet);
    }
    #endregion

    #region 开局
    /// <summary>校验开局条件（游戏币 + 进入消耗），不产生任何扣除。先判游戏币、再判进入消耗。</summary>
    public StartCondition CheckStartCondition()
    {
        if(!GameDataManager.Instance.HasProperty(PropertyType.GameCoin, bet))
            return StartCondition.NotEnoughGameCoin;
        if(!enterConfig.HasEnough(UIPanelIdSet.CrashSprintPanel))
            return StartCondition.NotEnoughStamina;
        return StartCondition.Ok;
    }

    /// <summary>
    /// 开始一局：先校验游戏币与进入消耗，满足才扣除两者、预生成隐藏爆点、倍率归零开始上涨。
    /// 开局消耗（体力等）统一由 GameEnterPanel 按 GameEnterPanelConfig 判断/扣除，本类不再自行持有该逻辑。
    /// warnTip 传给 TryConsume 兜底：正常情况下 CheckStartCondition 已提前拦下，仅在校验与扣除间发生资源变化的极端情况下才会触发。
    /// 返回开局条件——非 <see cref="StartCondition.Ok"/> 表示未开局（钱或消耗不足），由界面据此弹对应提示。
    /// </summary>
    public StartCondition StartRound(WarnTip warnTip)
    {
        if(state == GameState.Playing)
            return StartCondition.Ok;   // 已在进行中：忽略，无需提示

        StartCondition cond = CheckStartCondition();
        if(cond != StartCondition.Ok)
            return cond;

        GameDataManager.Instance.RemoveProperty(PropertyType.GameCoin, bet);
        enterConfig.TryConsume(UIPanelIdSet.CrashSprintPanel, warnTip);

        crashPoint = config.RollCrashPoint();
        currentMultiplier = 0f;
        stopMultiplier = 0f;

        SetState(GameState.Playing);
        OnRoundStart?.Invoke();

        // 爆点为 0.00：开局瞬间即触爆，直接判负，无侥幸空间
        if(crashPoint <= 0f)
            EndRound(false);

        return StartCondition.Ok;
    }
    #endregion

    #region 止盈 / 结算
    /// <summary>主动止盈：锁定当前倍率并以获胜结束本局。仅进行中有效。</summary>
    public void CashOut()
    {
        if(state != GameState.Playing)
            return;
        EndRound(true);
    }

    void EndRound(bool win)
    {
        stopMultiplier = win ? currentMultiplier : 0f;

        int payout = 0;
        if(win)
        {
            payout = Mathf.FloorToInt(bet * currentMultiplier);   // 收益 = 本金 × 止盈倍率
            GameDataManager.Instance.AddProperty(PropertyType.GameCoin, payout);
        }
        lastPayout = payout;

        SetState(GameState.Ended);
        OnRoundEnd?.Invoke(win, stopMultiplier, crashPoint, payout);
    }

    /// <summary>回到下注阶段，准备下一局。</summary>
    public void ResetToBetting()
    {
        if(state == GameState.Playing)
            return;
        currentMultiplier = 0f;
        SetState(GameState.Betting);
    }
    #endregion

    void SetState(GameState s)
    {
        state = s;
        OnStateChanged?.Invoke(s);
    }
}
