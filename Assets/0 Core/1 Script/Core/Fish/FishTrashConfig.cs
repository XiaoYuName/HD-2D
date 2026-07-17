using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 钓鱼垃圾（杂物）配表：走 CsvConfigAutoSync 自动同步管线（CSV 在 Data/Fish/FishTrashConfig.csv）。
/// Id = ItemData 里的杂物物品ID（131000~131004）；品质直接读 ItemData，故本表不再配置品质。
/// </summary>
[CreateAssetMenu(fileName = nameof(FishTrashConfig), menuName = ConfigMenuNameSet.MiniGame + nameof(FishTrashConfig))]
[CsvSyncedConfig]
public class FishTrashConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<long, FishTrashData> dataDict;   // Id(垃圾物品ID) → 行数据，由 CSV 自动导入
    [SerializeField] Object csvTable;                            // 拖入 FishTrashConfig.csv；变更自动同步，齿轮菜单可手动导入

    #region Get
    public Dictionary<long, FishTrashData> DataDict => dataDict;
    public bool Contains(long id) => dataDict.ContainsKey(id);
    public FishTrashData Get(long id) => dataDict[id];
    #endregion
}

[Serializable]
public class FishTrashData
{
    [SerializeField] long id;            // 垃圾物ID(=ItemData物品ID)
    [SerializeField] string remark;      // 名称
    [SerializeField] int appearWeight;   // 基础出现权重
    [SerializeField] int difficulty;     // 钓鱼难度

    public long Id => id;
    public string Remark => remark;
    public int AppearWeight => appearWeight;
    public int Difficulty => difficulty;
}
