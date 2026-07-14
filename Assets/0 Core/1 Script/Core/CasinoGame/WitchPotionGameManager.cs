using System;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 女巫毒药小游戏状态机：下注 → 生成药瓶 → 逐瓶开启 → 见好就收 / 开出毒药。
/// 只负责数据与规则，UI 由 <see cref="WitchPoisonPanel"/> 通过事件刷新。
/// </summary>
public class WitchPotionGameManager : MonoBehaviour
{
    public static WitchPotionGameManager St;

    [LabelText("配置")]
    [SerializeField] WitchPotionGameConfig config;
    [LabelText("进入消耗配置")]
    [SerializeField] GameEnterPanelConfig enterConfig;

    public WitchPotionGameConfig Config => config;
    public GameEnterPanelConfig EnterConfig => enterConfig;

    public enum GameState
    {
        /// <summary>下注阶段：可调下注金额，未开局。</summary>
        Betting,
        /// <summary>进行中：可逐瓶开启。</summary>
        Playing,
        /// <summary>本局结束：等待开始下一局。</summary>
        Ended,
    }

    #region 事件
    /// <summary>状态变化。</summary>
    public event Action<GameState> OnStateChanged;
    /// <summary>下注金额变化。</summary>
    public event Action<int> OnBetChanged;
    /// <summary>开局：参数为本局总瓶数。</summary>
    public event Action<int> OnRoundStart;
    /// <summary>开出一瓶：index、是否毒药、当前已开安全瓶数、当前倍率、当前可领取收益。</summary>
    public event Action<int, bool, int, float, int> OnBottleOpened;
    /// <summary>达到见好就收解锁条件。</summary>
    public event Action OnCashOutUnlocked;
    /// <summary>收益/倍率刷新：当前倍率、当前可领取收益。</summary>
    public event Action<float, int> OnPayoutChanged;
    /// <summary>本局结束：是否获胜、实际领取金币、本局是否享受女巫祝福。</summary>
    public event Action<bool, int, bool> OnRoundEnd;
    /// <summary>下一局是否已激活女巫祝福（用于 UI 显示）。</summary>
    public event Action<bool> OnBlessingChanged;
    #endregion

    #region 运行时状态
    GameState state = GameState.Betting;
    int bet;
    bool[] isPoison;       // 每个格子是否毒药
    bool[] opened;         // 每个格子是否已开
    int safeOpened;        // 已开启的安全瓶数
    bool cashOutUnlocked;

    // 跨局持久：连续失败计数与祝福状态
    int lossStreak;
    bool blessingActive;   // 本局是否享受祝福

    public GameState State => state;
    public int Bet => bet;
    public int SafeOpened => safeOpened;
    public bool CashOutUnlocked => cashOutUnlocked;
    public bool BlessingActive => blessingActive;
    public int TotalCount => config.TotalCount;

    /// <summary>当前倍率（已含祝福加成）。</summary>
    public float CurrentMultiplier
    {
        get
        {
            float m = config.GetMultiplier(safeOpened);
            if(blessingActive)
                m *= config.BlessingMultiplier;
            return m;
        }
    }

    /// <summary>当前可领取收益。</summary>
    public int CurrentPayout => Mathf.FloorToInt(bet * CurrentMultiplier);
    #endregion

    void Awake()
    {
        St = this;
        bet = config.MinBet;
    }

    #region 下注
    /// <summary>设置下注金额（按步进对齐并限制在区间内）。仅下注/结束阶段可调整。</summary>
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
    /// <summary>开始一局：扣除下注金币、随机布置药瓶。金币不足返回 false。</summary>
    public bool StartRound()
    {
        if(state == GameState.Playing)
            return false;

        if(!GameDataManager.Instance.HasProperty(PropertyType.GameCoin, bet))
        {
            return false;
        }

        // 开局消耗（体力等）统一由 GameEnterPanel 按 GameEnterPanelConfig 判断/扣除，本类不再自行持有该逻辑
        if(!enterConfig.TryConsume(UIPanelIdSet.WitchPoisonPanel))
            return false;

        GameDataManager.Instance.RemoveProperty(PropertyType.GameCoin, bet);

        int total = config.TotalCount;
        isPoison = new bool[total];
        opened = new bool[total];
        safeOpened = 0;
        cashOutUnlocked = false;

        PlacePoison(total, config.PoisonCount);

        // 祝福在开局时确定并消耗：达到失败连击则本局享受祝福
        blessingActive = lossStreak >= config.BlessingLossStreak;
        if(blessingActive)
            lossStreak = 0;

        SetState(GameState.Playing);
        OnRoundStart?.Invoke(total);
        OnBlessingChanged?.Invoke(blessingActive);
        OnPayoutChanged?.Invoke(CurrentMultiplier, CurrentPayout);
        return true;
    }

    // 随机抽取 poison 个位置放置毒药（Fisher-Yates 部分洗牌）
    void PlacePoison(int total, int poison)
    {
        int[] indices = new int[total];
        for(int i = 0; i < total; i++)
            indices[i] = i;

        for(int i = 0; i < poison; i++)
        {
            int j = UnityEngine.Random.Range(i, total);
            (indices[i], indices[j]) = (indices[j], indices[i]);
            isPoison[indices[i]] = true;
        }
    }
    #endregion

    #region 开瓶
    /// <summary>开启指定药瓶。返回是否成功开启（非法/已开/非进行中返回 false）。</summary>
    public bool OpenBottle(int index)
    {
        if(state != GameState.Playing)
            return false;
        if(index < 0 || index >= opened.Length || opened[index])
            return false;

        opened[index] = true;
        bool poison = isPoison[index];

        if(poison)
        {
            // 开出毒药：清空本局收益、判负、结束
            OnBottleOpened?.Invoke(index, true, safeOpened, 0f, 0);
            EndRound(false);
            return true;
        }

        safeOpened++;
        OnBottleOpened?.Invoke(index, false, safeOpened, CurrentMultiplier, CurrentPayout);
        OnPayoutChanged?.Invoke(CurrentMultiplier, CurrentPayout);

        if(!cashOutUnlocked && safeOpened >= config.CashOutUnlockSafeCount)
        {
            cashOutUnlocked = true;
            OnCashOutUnlocked?.Invoke();
        }

        // 所有安全瓶都开完：自动以满倍率结算获胜（无视见好就收解锁条件）
        if(safeOpened >= config.SafeCount)
            EndRound(true);

        return true;
    }
    #endregion

    #region 见好就收
    /// <summary>见好就收：领取当前收益并以获胜结束本局。未解锁时无效。</summary>
    public void CashOut()
    {
        if(state != GameState.Playing || !cashOutUnlocked)
            return;
            
        EndRound(true);
    }
    #endregion

    #region 结算
    void EndRound(bool win)
    {
        int payout = 0;
        if(win)
        {
            payout = CurrentPayout;
            GameDataManager.Instance.AddProperty(PropertyType.GameCoin, payout);
        }

        // 刷新连续失败计数：失败累计、获胜清零
        if(win)
            lossStreak = 0;
        else
            lossStreak++;

        bool blessedThisRound = blessingActive;
        SetState(GameState.Ended);
        OnRoundEnd?.Invoke(win, payout, blessedThisRound);

        // 提示下一局祝福状态
        bool nextBlessing = lossStreak >= config.BlessingLossStreak;
        OnBlessingChanged?.Invoke(nextBlessing);
    }

    /// <summary>回到下注阶段，准备下一局。</summary>
    public void ResetToBetting()
    {
        if(state == GameState.Playing)
            return;
        SetState(GameState.Betting);
    }
    #endregion

    void SetState(GameState s)
    {
        state = s;
        OnStateChanged?.Invoke(s);
    }
}
