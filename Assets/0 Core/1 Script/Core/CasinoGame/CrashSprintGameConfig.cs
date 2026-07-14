using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 爆点冲刺（中档）小游戏配置：下注区间、倍率上限与匀速增长速度、爆点概率分布、再来一局消耗。
/// 通过菜单 MiniGame/CrashSprintGameConfig 创建资产，挂到 <see cref="CrashSprintGameManager"/> 上。
/// </summary>
[CreateAssetMenu(fileName = "CrashSprintGameConfig", menuName = "MiniGame/CrashSprintGameConfig")]
public class CrashSprintGameConfig : ScriptableObject
{
    [Title("下注")]
    [LabelText("最低下注"), MinValue(0)][SerializeField] int minBet = 100;
    [LabelText("最高下注"), MinValue(0)][SerializeField] int maxBet = 1000;
    [LabelText("下注步进"), MinValue(1)][SerializeField] int betStep = 100;

    [Title("倍率")]
    [LabelText("倍率上限"), MinValue(0.01f)][SerializeField] float maxMultiplier = 5f;
    [LabelText("每秒增长倍率（匀速）"), MinValue(0.01f)] [SerializeField] float growthPerSecond = 1f;

    [Title("爆点概率分布")]
    [InfoBox("开局按权重随机落入某个区间，再在区间内均匀取值（保留两位小数）。各区间权重之和无需为 100，按比例归一。\n默认：0.00-1.00=15% | 1.01-2.00=30% | 2.01-3.00=20% | 3.01-4.00=20% | 4.01-5.00=15%")]
    [LabelText("爆点区间")][TableList(AlwaysExpanded = true)][SerializeField] List<CrashBucket> crashBuckets = new List<CrashBucket>
    {
        new CrashBucket(0.00f, 1.00f, 15f),
        new CrashBucket(1.01f, 2.00f, 30f),
        new CrashBucket(2.01f, 3.00f, 20f),
        new CrashBucket(3.01f, 4.00f, 20f),
        new CrashBucket(4.01f, 5.00f, 15f),
    };

    #region Get
    public int MinBet => minBet;
    public int MaxBet => Mathf.Max(minBet, maxBet);
    public int BetStep => Mathf.Max(1, betStep);
    public float MaxMultiplier => Mathf.Max(0.01f, maxMultiplier);
    public float GrowthPerSecond => Mathf.Max(0.01f, growthPerSecond);
    #endregion

    /// <summary>
    /// 开局随机生成本局爆点：先按权重选区间，再在区间内均匀取值并保留两位小数，限制在 [0, 倍率上限]。
    /// </summary>
    public float RollCrashPoint()
    {
        if(crashBuckets == null || crashBuckets.Count == 0)
            return MaxMultiplier;

        float total = 0f;
        foreach(CrashBucket b in crashBuckets)
            total += Mathf.Max(0f, b.Weight);
        if(total <= 0f)
            return MaxMultiplier;

        float r = UnityEngine.Random.Range(0f, total);
        float acc = 0f;
        foreach(CrashBucket b in crashBuckets)
        {
            acc += Mathf.Max(0f, b.Weight);
            if(r < acc)
                return RollInBucket(b);
        }
        return RollInBucket(crashBuckets[crashBuckets.Count - 1]);   // 浮点兜底
    }

    // 区间内均匀取值并保留两位小数
    float RollInBucket(CrashBucket b)
    {
        float v = UnityEngine.Random.Range(b.Min, b.Max);
        v = Mathf.Clamp(v, 0f, MaxMultiplier);
        return Mathf.Round(v * 100f) / 100f;
    }

    /// <summary>单个爆点概率区间：[Min, Max] 与权重。</summary>
    [Serializable]
    public class CrashBucket
    {
        [LabelText("下限"), MinValue(0f)] public float Min;
        [LabelText("上限"), MinValue(0f)] public float Max;
        [LabelText("权重"), MinValue(0f)] public float Weight;

        public CrashBucket() { }
        public CrashBucket(float min, float max, float weight)
        {
            Min = min;
            Max = max;
            Weight = weight;
        }
    }
}
