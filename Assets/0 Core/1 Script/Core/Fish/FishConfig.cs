using System;
using System.Collections.Generic;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = nameof(FishConfig), menuName = ConfigMenuNameSet.MiniGame + nameof(FishConfig))]
[CsvSyncedConfig]
public class FishConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<long, FishItemData> dataDict;
    [SerializeField] Object csvTable;

    [LabelText("钓鱼控制条移动速度")][SerializeField] float fishCatchCtrlBarMoveSpeed;
    [LabelText("进度条每秒上升量")][SerializeField] float fishProgressBarSpeed;
    [LabelText("鱼移动随机最小时间")][SerializeField] float fishMoveMinTime;
    [LabelText("鱼移动随机最大时间")][SerializeField] float fishMoveMaxTime;
    [LabelText("鱼移动曲线")][SerializeField] TweenSettings fishMoveTs;
    [LabelText("捕获进度初始值")][SerializeField] float catchPointStart = 10f;
    [LabelText("上钩后响应窗口(秒)")][SerializeField] float responseWindow = 3f;

    #region Get
    public Dictionary<long, FishItemData> DataDict => dataDict;
    public bool Contains(long id) => dataDict.ContainsKey(id);
    public FishItemData Get(long id) => dataDict[id];
    public float FishCatchCtrlBarMoveSpeed => fishCatchCtrlBarMoveSpeed;
    public float FishProgressBarSpeed => fishProgressBarSpeed;
    public float FishMoveMinTime => fishMoveMinTime;
    public float FishMoveMaxTime => fishMoveMaxTime;
    public TweenSettings FishMoveTs => fishMoveTs;
    public float CatchPointStart => catchPointStart;
    public float ResponseWindow => responseWindow;
    #endregion
}

[Serializable]
public class FishItemData
{
    [SerializeField] long id;                          // 鱼ID
    [SerializeField] string remark;                    // 鱼种名称
    [SerializeField] int unlockLevel;                  // 最低解锁钓鱼等级
    [SerializeField] List<long> allowedRods;           // 允许使用的鱼竿（鱼竿物品ID，对应 FishRodConfig）
    [SerializeField] List<TimeSlot> timeSlots;  // 可垂钓游戏时段
    [SerializeField] FishBodyType bodyType;            // 鱼类体型分类
    [SerializeField] float lengthMin;                  // 标准长度区间下限(cm)
    [SerializeField] float lengthMax;                  // 标准长度区间上限(cm)
    [SerializeField] float weightFactor;               // 固定换算系数
    [SerializeField] float weightMin;                  // 常规捕获参考重量下限(kg)
    [SerializeField] float weightMax;                  // 常规捕获参考重量上限(kg)
    [SerializeField] float perfectWeightMax;           // 满级完美捕获上限重量(kg)
    [SerializeField] int appearWeight;                 // 基础出现权重
    [SerializeField] int difficulty;                   // 钓鱼难度
    [SerializeField] int quality;                      // 品质

    public long Id => id;
    public string Remark => remark;
    public int UnlockLevel => unlockLevel;
    public List<long> AllowedRods => allowedRods;
    public List<TimeSlot> TimeSlots => timeSlots;
    public FishBodyType BodyType => bodyType;
    public float LengthMin => lengthMin;
    public float LengthMax => lengthMax;
    public float WeightFactor => weightFactor;
    public float WeightMin => weightMin;
    public float WeightMax => weightMax;
    public float PerfectWeightMax => perfectWeightMax;
    public int AppearWeight => appearWeight;
    public int Difficulty => difficulty;
    public int Quality => quality;
    public bool CanCatchAt(TimeSlot mode) => timeSlots.Contains(mode);
}

public enum FishBodyType
{
    [LabelText("小型细长鱼类")]
    SmallSlim = 0,
    [LabelText("常规中型鱼类")]
    Medium = 1,
    [LabelText("大型肥厚鱼类")]
    LargeThick = 2,
    [LabelText("传奇鱼类")]
    Legendary = 3,
}
