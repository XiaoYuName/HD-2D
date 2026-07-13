// dataDict / csvTable 参照 CsvConfigCodeGen 生成风格（Project 选中 ShopHelpGameItemConfig.csv → 右键「CSV 生成配置类」可重生成覆盖 ShopHelpGameItemData）。
// 手工调整：下方「玩法数值」区为手写字段，不由 CSV 导入（CsvConfigAutoSync 只覆盖 dataDict）；重新生成时请保留本注释与该区。
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 商店帮忙小游戏配置：货物图标表（按物品 ItemID 配图标，CSV 导入）+ 单局玩法数值。
/// 货物来自 <see cref="XFramework.TbSuperMarketShopData"/>（运行时随机取 2~4 种），其图标由本表按 ItemID 指定（不取物品数据库图标）。
/// 进入/再来一局的消耗不在此配置，统一来源于 GameEnterPanelConfig 的对应面板条目。
/// 通过菜单 Configs/MiniGame/ShopHelpGameConfig 创建资产，挂到 <see cref="ShopHelpGameManager"/> 上，并把 ShopHelpGameItemConfig.csv 拖到 csvTable。
/// </summary>
[CreateAssetMenu(fileName = "ShopHelpGameConfig", menuName = "Configs/MiniGame/ShopHelpGameConfig")]
[CsvSyncedConfig]
public class ShopHelpGameConfig : SerializedScriptableObject
{
    [Title("货物图标（CSV 导入，按 ItemID）")]
    [SerializeField] Dictionary<string, ShopHelpGameItemData> dataDict;   // ItemID(字符串) → 行数据，由 CSV 自动导入
    [SerializeField] Object csvTable;                                     // 拖入 ShopHelpGameItemConfig.csv；变更自动同步

    [Title("玩法数值（手工配置）")]
    [LabelText("单局倒计时(秒)"), MinValue(1)]
    [SerializeField] int countdownSeconds = 10;
    [LabelText("货架行数"), MinValue(1)]
    [SerializeField] int rows = 4;
    [LabelText("货架列数"), MinValue(1)]
    [SerializeField] int cols = 5;

    [Title("货物品类")]
    [LabelText("最少品类数"), MinValue(1)]
    [SerializeField] int minGoodsTypes = 2;
    [LabelText("最多品类数"), MinValue(1)]
    [SerializeField] int maxGoodsTypes = 4;
    [LabelText("每类数量随机浮动(±)"), MinValue(0)]
    [SerializeField] int quantityRandomRange = 3;

    [Title("胜利奖励")]
    [LabelText("获得金币(GameCoin)"), MinValue(0)]
    [SerializeField] int rewardCoin = 100;
    [LabelText("获得好感度"), MinValue(0)]
    [SerializeField] int rewardFavor = 5;

    #region Get
    public IReadOnlyDictionary<string, ShopHelpGameItemData> DataDict => dataDict;

    /// <summary>按物品 ItemID 取配置的图标 AA Key；未配置返回 null。</summary>
    public string GetIconPath(long itemId)
        => dataDict != null && dataDict.TryGetValue(itemId.ToString(), out ShopHelpGameItemData d) ? d.IconPath : null;

    public int CountdownSeconds => Mathf.Max(1, countdownSeconds);
    public int Rows => Mathf.Max(1, rows);
    public int Cols => Mathf.Max(1, cols);
    /// <summary>货架格子总数（= 行 × 列），也是本局需要摆放的货物总数。</summary>
    public int TotalSlots => Rows * Cols;

    public int MinGoodsTypes => Mathf.Clamp(minGoodsTypes, 1, MaxGoodsTypes);
    public int MaxGoodsTypes => Mathf.Max(1, maxGoodsTypes);
    public int QuantityRandomRange => Mathf.Max(0, quantityRandomRange);

    public int RewardCoin => Mathf.Max(0, rewardCoin);
    public int RewardFavor => Mathf.Max(0, rewardFavor);
    #endregion
}

[System.Serializable]
public class ShopHelpGameItemData
{
    [SerializeField] long id;          // 物品 ItemID
    [SerializeField] string iconPath;  // 图标 AA Key

    public long Id => id;
    public string IconPath => iconPath;
}
