using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;
using XFramework;

[CreateAssetMenu(fileName = "TimeSlotConfig", menuName = "Configs/MainUI/TimeSlotConfig")]
[CsvSyncedConfig]
public class TimeSlotConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<string, TimeSlotItemData> dataDict;   // Id(枚举名) → 行数据，由 CSV 自动导入
    [SerializeField] Object csvTable;

    public Dictionary<string, TimeSlotItemData> DataDict => dataDict;

    public TimeSlotItemData Get(TimeSlot mode) => dataDict.TryGetValue(mode.ToString(), out TimeSlotItemData d) ? d : null;

    public string GetNameKey(TimeSlot mode) => Get(mode).NameKey;
    public string GetIconKey(TimeSlot mode) => Get(mode).IconKey;

    // 组合 Assets/AddressableAssets/Remote/Prefabs/UGUI/MainUI 下的图标资源路径（AssetsPaths.MainUIIconPath + IconKey + 扩展名）
    public string GetIconPath(TimeSlot mode) => AssetsPaths.MainUIIconPath + GetIconKey(mode) + PicSuffix;
    const string PicSuffix = ".png"; 
}

[Serializable]
public class TimeSlotItemData
{
    [SerializeField] string id;   // Id
    [SerializeField] string nameKey;   // 名称Key
    [SerializeField] string iconKey;   // 图标Key

    public string Id => id;
    public string NameKey => nameKey;
    public string IconKey => iconKey;
}
