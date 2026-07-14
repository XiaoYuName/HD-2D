using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopHelpGameConfig", menuName = "Configs/MiniGame/ShopHelpGameConfig")]
public class ShopHelpGameConfig : SerializedScriptableObject
{
    [Title("玩法数值（手工配置）")]
    [LabelText("单局倒计时(秒)"), MinValue(1)]
    [SerializeField] int countdownSeconds = 10;

    [Title("货物品类")]
    [LabelText("最少品类数"), MinValue(1)]
    [SerializeField] int minGoodsTypes = 2;
    [LabelText("最多品类数"), MinValue(1)]
    [SerializeField] int maxGoodsTypes = 4;
    [LabelText("每类数量随机浮动(±)"), MinValue(0)]
    [SerializeField] int quantityRandomRange = 3;

    [Title("胜利奖励")]
    [LabelText("获得金币(Coin)"), MinValue(0)]
    [SerializeField] int rewardCoin = 100;
    [LabelText("获得好感度"), MinValue(0)]
    [SerializeField] int rewardFavor = 5;

    /// <summary>货架格子总数：固定 16 个（场景中手动摆放对应数量的 <see cref="ShopHelpItemCellUI"/>），不再由配置决定。</summary>
    public const int TotalSlots = 16;

    #region Get
    public int CountdownSeconds => Mathf.Max(1, countdownSeconds);
    public int MinGoodsTypes => Mathf.Clamp(minGoodsTypes, 1, MaxGoodsTypes);
    public int MaxGoodsTypes => Mathf.Max(1, maxGoodsTypes);
    public int QuantityRandomRange => Mathf.Max(0, quantityRandomRange);
    public int RewardCoin => Mathf.Max(0, rewardCoin);
    public int RewardFavor => Mathf.Max(0, rewardFavor);
    #endregion
}
