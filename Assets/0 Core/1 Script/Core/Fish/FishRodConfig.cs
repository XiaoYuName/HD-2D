using UnityEngine;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "FishRodConfig", menuName = "Configs/FishRodConfig")]
[CsvSyncedConfig]
public class FishRodConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<long, FishRodData> rodDataDict;   // Id(鱼竿物品ID) → 行数据，由 CSV 自动导入
    [SerializeField] Object csvTable;                             // 拖入对应 CSV；变更自动同步，齿轮菜单可手动导入

    public Dictionary<long, FishRodData> RodDataDict => rodDataDict;
    
    public bool Contains(long id)
    {
        return rodDataDict.ContainsKey(id);
    }
}

[Serializable]
public class FishRodData
{
    public int Id;
    public string Remark;
}