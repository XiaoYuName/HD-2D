// 本文件由 CsvConfigCodeGen 根据 Assets/0 Core/1 Script/Data/Common/GameEnterPanelConfig.csv 表头自动生成，表头变更后可重新生成覆盖（手工改动会被一并覆盖）。
// 手工调整：menuName 保留在 Configs/MiniGame 下；PropertyType 枚举来自 XFramework，需补 using；
// Consumes 列类型为 Dictionary<PropertyType,int>（单元格写 "Strength:50" 或 "Strength:50;ActionPointsValue:2"），
// 支持一行同时配置多种资源消耗，由 CsvConfigAutoSync 的 Dictionary<TKey,TValue> 解析支持（见该文件 TryParseCell）。
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
