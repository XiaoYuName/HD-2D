using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "EnvironmentModeConfig", menuName = "Configs/MainUI/EnvironmentModeConfig")]
[CsvSyncedConfig]
public class EnvironmentModeConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<string, EnvironmentModeItemData> dataDict;   // Id(枚举名) → 行数据，由 CSV 自动导入
    [SerializeField] Object csvTable;

    public Dictionary<string, EnvironmentModeItemData> DataDict => dataDict;

    public EnvironmentModeItemData Get(EnvironmentMode mode) => dataDict.TryGetValue(mode.ToString(), out EnvironmentModeItemData d) ? d : null;

    public string GetNameKey(EnvironmentMode mode) => Get(mode).NameKey;
    public string GetIconKey(EnvironmentMode mode) => Get(mode).IconKey;

    // 组合 Assets/AddressableAssets/Remote/Prefabs/UGUI/MainUI 下的图标资源路径（AssetsPaths.MainUIIconPath + IconKey + 扩展名）
    public string GetIconPath(EnvironmentMode mode) => AssetsPaths.MainUIIconPath + GetIconKey(mode) + PicSuffix;
    const string PicSuffix = ".png"; 
}

[Serializable]
public class EnvironmentModeItemData
{
    [SerializeField] string id;   // Id
    [SerializeField] string nameKey;   // 名称Key
    [SerializeField] string iconKey;   // 图标Key

    public string Id => id;
    public string NameKey => nameKey;
    public string IconKey => iconKey;
}
