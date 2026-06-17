using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 女巫毒药小游戏配置：行列数、毒药数量、各瓶倍率、下注区间、女巫祝福规则。
/// 通过菜单 MiniGame/WitchPotionGameConfig 创建资产，挂到 WitchPotionGameManager 上。
/// </summary>
[CreateAssetMenu(fileName = "WitchPotionGameConfig", menuName = "MiniGame/WitchPotionGameConfig")]
public class WitchPotionGameConfig : ScriptableObject
{
    [Title("棋盘")]
    [LabelText("行数"), MinValue(1)]
    [SerializeField] int rows = 3;
    [LabelText("列数"), MinValue(1)]
    [SerializeField] int cols = 4;
    [LabelText("毒药数量"), MinValue(1)]
    [SerializeField] int poisonCount = 2;

    [Title("下注")]
    [LabelText("最低下注"), MinValue(0)]
    [SerializeField] int minBet = 1000;
    [LabelText("最高下注"), MinValue(0)]
    [SerializeField] int maxBet = 10000;
    [LabelText("下注步进"), MinValue(1)]
    [SerializeField] int betStep = 100;

    [Title("倍率")]
    [LabelText("解锁见好就收所需安全瓶数"), MinValue(1)]
    [SerializeField] int cashOutUnlockSafeCount = 3;
    [InfoBox("索引0=开出第1瓶安全瓶时的倍率，索引1=第2瓶……开出瓶数超出列表长度时取最后一项。\n规则示例：第3瓶=2，第4瓶=2.5，第5瓶=3，第6瓶=5，第7瓶=10。")]
    [LabelText("各安全瓶累计倍率")]
    [SerializeField] List<float> safeMultipliers = new List<float> { 1.2f, 1.5f, 2f, 2.5f, 3f, 5f, 10f };

    [Title("女巫祝福 Buff")]
    [LabelText("连续失败多少局后触发祝福"), MinValue(1)]
    [SerializeField] int blessingLossStreak = 3;
    [LabelText("祝福倍率（对最终倍率额外相乘）"), MinValue(1f)]
    [SerializeField] float blessingMultiplier = 2f;

    [Title("结算")]
    [LabelText("再来一局消耗体力"), MinValue(0)]
    [SerializeField] int playAgainSpCost = 30;

    #region Get
    public int Rows => Mathf.Max(1, rows);
    public int Cols => Mathf.Max(1, cols);
    public int TotalCount => Rows * Cols;
    /// <summary>毒药数量，至少 1 且不超过总数-1（至少留一瓶安全瓶）。</summary>
    public int PoisonCount => Mathf.Clamp(poisonCount, 1, Mathf.Max(1, TotalCount - 1));
    public int SafeCount => TotalCount - PoisonCount;

    public int MinBet => minBet;
    public int MaxBet => Mathf.Max(minBet, maxBet);
    public int BetStep => Mathf.Max(1, betStep);

    public int CashOutUnlockSafeCount => cashOutUnlockSafeCount;
    public int BlessingLossStreak => blessingLossStreak;
    public float BlessingMultiplier => Mathf.Max(1f, blessingMultiplier);
    public int PlayAgainSpCost => Mathf.Max(0, playAgainSpCost);

    /// <summary>
    /// 取开出 safeOpened 瓶安全瓶时的累计倍率。safeOpened 从 1 开始；0 或更小返回 0。
    /// 超出配置长度时取最后一项。
    /// </summary>
    public float GetMultiplier(int safeOpened)
    {
        if(safeOpened <= 0 || safeMultipliers == null || safeMultipliers.Count == 0)
            return 0f;
        int idx = Mathf.Clamp(safeOpened - 1, 0, safeMultipliers.Count - 1);
        return safeMultipliers[idx];
    }
    #endregion
}
