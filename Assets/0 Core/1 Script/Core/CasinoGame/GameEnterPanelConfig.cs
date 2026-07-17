// 本文件由 CsvConfigCodeGen 根据 Assets/0 Core/1 Script/Data/Common/GameEnterPanelConfig.csv 表头自动生成，表头变更后可重新生成覆盖（手工改动会被一并覆盖）。
// 手工调整：menuName 保留在 Configs/MiniGame 下；PropertyType 枚举来自 XFramework，需补 using；
// Consumes 列类型为 Dictionary<PropertyType,int>（单元格写 "Strength:50" 或 "Strength:50;ActionPointsValue:2"），
// 支持一行同时配置多种资源消耗，由 CsvConfigAutoSync 的 Dictionary<TKey,TValue> 解析支持（见该文件 TryParseCell）。
// 手工调整：进入消耗的判断/扣除逻辑（原在 GameEnterPanel 静态方法中，唯一数据源）迁移至此，
// 供 GameEnterPanel、ShopHelpEnterPanel 等各入口面板直接持有 config 引用调用，无需再依赖 GameEnterPanel 的单例。
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "GameEnterPanelConfig", menuName = "Configs/MiniGame/GameEnterPanelConfig")]
[CsvSyncedConfig]
public class GameEnterPanelConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<string, GameEnterPanelItemData> dataDict;   // Id → 行数据，由 CSV 自动导入
    [SerializeField] Object csvTable;                                       // 拖入对应 CSV；变更自动同步，齿轮菜单可手动导入

    public Dictionary<string, GameEnterPanelItemData> DataDict => dataDict;

    #region 进入消耗（唯一数据源，供各入口面板/小游戏 Manager 按 panelId 读取本表 Consumes 校验/扣除）
    bool TryGetConsumes(string panelId, out Dictionary<PropertyType, int> consumes)
    {
        consumes = null;
        return dataDict != null && dataDict.TryGetValue(panelId, out GameEnterPanelItemData d) && (consumes = d.Consumes) != null;
    }

    /// <summary>某面板配置的具体消耗数值（用于 UI 展示，如结算面板的「再来一局消耗体力」）；未配置该资源类型返回 0。</summary>
    public int GetConsume(string panelId, PropertyType type)
    {
        if(!TryGetConsumes(panelId, out Dictionary<PropertyType, int> consumes))
            return 0;
        return consumes.TryGetValue(type, out int v) ? v : 0;
    }

    /// <summary>玩家资源是否满足某面板的进入消耗（不扣除）；未配置该面板视为无消耗，恒为 true。</summary>
    public bool HasEnough(string panelId) => HasEnough(panelId, out _);

    /// <summary>校验某面板的进入消耗但不扣除；不足时若传入 warnTip，直接弹出对应的「XX不足」提示。</summary>
    public bool HasEnough(string panelId, WarnTip warnTip)
    {
        if(HasEnough(panelId, out PropertyType lackType))
            return true;
        warnTip?.Show(LocTableSet.GameEnterPanel, NotEnoughKey(lackType));
        return false;
    }

    public bool HasEnough(string panelId, out PropertyType lackType)
    {
        lackType = default;
        if(!TryGetConsumes(panelId, out Dictionary<PropertyType, int> consumes))
            return true;
        foreach(KeyValuePair<PropertyType, int> kv in consumes)
        {
            if(!GameDataManager.Instance.HasProperty(kv.Key, kv.Value))
            {
                lackType = kv.Key;
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 校验并扣除某面板的进入消耗；一行可配置多种资源，任一不足则整体拦截、不产生任何扣除。
    /// warnTip 必传：不足时直接弹出对应的「XX不足」提示，逼迫所有消耗入口都走统一提示配置。
    /// </summary>
    public bool TryConsume(string panelId, WarnTip warnTip)
    {
        // todo 行动力消耗时，禁用时间更新
        if(!HasEnough(panelId, out PropertyType lackType))
        {
            warnTip.Show(LocTableSet.GameEnterPanel, NotEnoughKey(lackType));
            return false;
        }
        if(TryGetConsumes(panelId, out Dictionary<PropertyType, int> consumes))
            foreach(KeyValuePair<PropertyType, int> kv in consumes)
                GameDataManager.Instance.RemoveProperty(kv.Key, kv.Value);
        return true;
    }

    // 按消耗的资源类型取对应的「不足」提示 Key；新增 PropertyType 需在此补充映射
    static readonly Dictionary<PropertyType, string> notEnoughKeyMap = new ()
    {
        { PropertyType.Strength, LocVarSet.MiniGame.NotEnoughStamina },
        { PropertyType.GameCoin, LocVarSet.MiniGame.NotEnoughGameCoin },
        { PropertyType.ActionPointsValue, LocVarSet.MiniGame.NotEnoughAp },
    };

    static string NotEnoughKey(PropertyType type) => notEnoughKeyMap[type];
    #endregion
}

[Serializable]
public class GameEnterPanelItemData
{
    [SerializeField] string id;   // Id
    [SerializeField] string nameKey;   // 名称Key
    [SerializeField] string remark;   // 备注
    [SerializeField] string descKey;   // 描述Key
    [SerializeField] string iconPath;   // 图标路径
    [SerializeField] Dictionary<PropertyType, int> consumes;   // 消耗（Type:Value，多个用;分隔）
    [SerializeField] Color topColor;   // 顶部颜色

    public string Id => id;
    public string NameKey => nameKey;
    public string Remark => remark;
    public string DescKey => descKey;
    public string IconPath => iconPath;
    public Dictionary<PropertyType, int> Consumes => consumes;
    public Color TopColor => topColor;
}
