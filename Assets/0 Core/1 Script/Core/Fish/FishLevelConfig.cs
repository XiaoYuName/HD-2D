using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 钓鱼升级配表：走 CsvConfigAutoSync 自动同步（CSV 在 Data/Fish/FishLevelConfig.csv）。
/// Id = 钓鱼等级；配置每级升级所需经验（策划案 5.7）与该级永久加成（策划案 5.6：遛鱼绿条加宽、咬钩时间减免）。
/// 满级（Lv10）ExpToNext = 0，表示不再累计经验。
/// </summary>
[CreateAssetMenu(fileName = nameof(FishLevelConfig), menuName = ConfigMenuNameSet.MiniGame + nameof(FishLevelConfig))]
[CsvSyncedConfig]
public class FishLevelConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<int, FishLevelData> dataDict;   // 等级 → 行数据，由 CSV 自动导入
    [SerializeField] Object csvTable;                           // 拖入 FishLevelConfig.csv

    #region Get
    public Dictionary<int, FishLevelData> DataDict => dataDict;
    public bool Contains(int level) => dataDict.ContainsKey(level);
    public FishLevelData Get(int level) => dataDict.TryGetValue(level, out FishLevelData d) ? d : null;

    /// <summary>全表最高等级（满级）。</summary>
    public int MaxLevel
    {
        get
        {
            int max = 1;
            foreach(int lv in dataDict.Keys)
                if(lv > max) max = lv;
            return max;
        }
    }
    #endregion
}

[Serializable]
public class FishLevelData
{
    [SerializeField] int id;                     // 钓鱼等级
    [SerializeField] int expToNext;              // 升到下一级所需经验（满级为0）
    [SerializeField] int greenBarWidthBonus;     // 遛鱼绿条加宽(px，该级总加成)
    [SerializeField] int biteTimeReducePercent;  // 咬钩等待时间减免(%，该级总减免)

    public int Id => id;
    public int ExpToNext => expToNext;
    public int GreenBarWidthBonus => greenBarWidthBonus;
    public int BiteTimeReducePercent => biteTimeReducePercent;
}
