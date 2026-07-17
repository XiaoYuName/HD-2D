using System;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 钓鱼系统常驻管理器：持有鱼竿/升级配置，维护钓鱼等级、当前等级内累计经验、当前装备鱼竿。
/// 实现 <see cref="ISaveable"/>，须与 GameDataManager 一样常驻（启动/常驻场景），在 Start 注册到存档系统。
/// 对外提供：钓鱼难度计算（鱼难度 - 鱼竿难度降低）、经验累加与升级、等级永久加成（绿条加宽/咬钩时间减免）。
/// </summary>
public class FishGameManager : MonoBehaviour, ISaveable
{
    [LabelText("鱼竿配置")][SerializeField] FishRodConfig rodConfig;
    [LabelText("钓鱼升级配置")][SerializeField] FishLevelConfig levelConfig;

    [ShowInInspector, ReadOnly] int level = 1;
    [ShowInInspector, ReadOnly] int exp;                 // 当前等级内累计经验
    [ShowInInspector, ReadOnly] long currentRodId = 140000;   // 默认竹鱼竿

    /// <summary>等级 / 经验变化，UI 据此刷新。</summary>
    public event Action OnProgressChanged;

    public int Level => level;
    public int Exp => exp;
    public long CurrentRodId { get => currentRodId; set { currentRodId = value; OnProgressChanged?.Invoke(); } }
    public int MaxLevel => levelConfig.MaxLevel;
    public bool IsMaxLevel => level >= MaxLevel;

    #region ISaveable
    public string GUID => "FishGameManager";
    void Start() => SaveGameManager.Instance.RegisterSaveable(this);

    public void SaveData(GameSaveData data)
    {
        data.FishGame.Level = level;
        data.FishGame.Exp = exp;
        data.FishGame.CurrentRodId = currentRodId;
    }

    public void LoadData(GameSaveData data)
    {
        level = data.FishGame.Level;
        exp = data.FishGame.Exp;
        currentRodId = data.FishGame.CurrentRodId;
        OnProgressChanged?.Invoke();
    }
    #endregion

    #region 难度 / 等级加成
    /// <summary>当前鱼竿对钓鱼难度的降低值。</summary>
    public int RodDifficultyReduction => rodConfig.GetDifficultyReduction(currentRodId);

    /// <summary>实际钓鱼难度 = 鱼本身难度 - 当前鱼竿难度降低（下限 1）。同时作为 QTE 捕获进度的目标点。</summary>
    public int CalcFishDifficulty(int fishDifficulty) => Mathf.Max(1, fishDifficulty - RodDifficultyReduction);

    /// <summary>当前等级的遛鱼绿条加宽(px)（策划案 5.6）。</summary>
    public int GreenBarWidthBonus => levelConfig.Get(level)?.GreenBarWidthBonus ?? 0;

    /// <summary>当前等级的咬钩等待时间减免比例(0~1)（策划案 5.6）。</summary>
    public float BiteTimeReduceFactor => Mathf.Clamp01((levelConfig.Get(level)?.BiteTimeReducePercent ?? 0) / 100f);
    #endregion

    #region 经验 / 升级
    // 品质系数（策划案 5.1.1/5.2；原案数值有笔误，此处按 0.5 等差取值，可调）
    static float QualityCoef(int quality) => quality switch
    {
        <= 1 => 0f,
        2 => 0.5f,
        3 => 1f,
        4 => 1.5f,
        _ => 2f,
    };

    /// <summary>本次渔获的基础经验（策划案 5.2）。杂物固定 3 点。</summary>
    public float CalcBaseExp(int quality, int difficulty, bool isFish)
        => isFish ? (QualityCoef(quality) + 1f) * 3f + difficulty / 3f : 3f;

    /// <summary>
    /// 结算加经验：最终经验 = 基础经验 ×(1 + 完美加成)，完美捕获经验倍率 ×2。
    /// 满级后经验永久为 0（策划案 5.7）。返回本次实际获得的经验。
    /// </summary>
    public int AddExp(int quality, int difficulty, bool isFish, bool perfect)
    {
        if(IsMaxLevel)
            return 0;

        int gain = Mathf.RoundToInt(CalcBaseExp(quality, difficulty, isFish) * (perfect ? 2f : 1f));
        exp += gain;

        // 逐级结算升级（每级重新累计）
        while(!IsMaxLevel)
        {
            int need = levelConfig.Get(level)?.ExpToNext ?? 0;
            if(need <= 0 || exp < need)
                break;
            exp -= need;
            level++;
        }
        if(IsMaxLevel)
            exp = 0;

        OnProgressChanged?.Invoke();
        return gain;
    }
    #endregion
}
