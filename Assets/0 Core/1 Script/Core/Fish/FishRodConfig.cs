using UnityEngine;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = nameof(FishRodConfig), menuName = ConfigMenuNameSet.MiniGame + nameof(FishRodConfig))]
[CsvSyncedConfig]
public class FishRodConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<long, FishRodData> rodDataDict;   // Id(鱼竿物品ID) → 行数据，由 CSV 自动导入
    [SerializeField] Object csvTable;                             // 拖入对应 CSV；变更自动同步，齿轮菜单可手动导入

    public Dictionary<long, FishRodData> RodDataDict => rodDataDict;

    public bool Contains(long id) => rodDataDict.ContainsKey(id);
    public FishRodData Get(long id) => rodDataDict[id];

    /// <summary>该鱼竿对「鱼的钓鱼难度」的降低值；无配置返回 0。</summary>
    public int GetDifficultyReduction(long id) => rodDataDict.TryGetValue(id, out FishRodData d) ? d.DifficultyReduction : 0;
}

[Serializable]
public class FishRodData
{
    public int Id;
    public string Remark;
    public int DifficultyReduction;   // 鱼的难度降低（竹0/玻璃30/铱金50/高级100）
}