using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework.Fish
{
    /// <summary>
    /// 钓鱼系统常驻管理器：持有鱼竿/升级配置，维护钓鱼等级、当前等级内累计经验、当前装备鱼竿。
    /// 实现 <see cref="ISaveable"/>，须与 GameDataManager 一样常驻（启动/常驻场景），在 Start 注册到存档系统。
    /// 对外提供：钓鱼难度计算（鱼难度 - 鱼竿难度降低）、经验累加与升级、等级永久加成（绿条加宽/咬钩时间减免）。
    /// </summary>
    public class FishGameManager : MonoSingleton<FishGameManager>
    {
        [LabelText("鱼竿配置")][SerializeField] FishRodConfig rodConfig;
        [LabelText("钓鱼升级配置")][SerializeField] FishLevelConfig levelConfig;

        [ShowInInspector, ReadOnly] long currentRodId = 140000;   // 默认竹鱼竿

        /// <summary>等级 / 经验变化，UI 据此刷新。</summary>
        public event Action OnProgressChanged;

        /// <summary>等级文本前缀，UI 显示等级时统一加上（如 "Lv 3"）。</summary>
        public const string LvPrefix = "Lv ";

        public int Level => GetProgressValue(PropertyType.FishLevel);
        public int Exp => GetProgressValue(PropertyType.FishExp);
        public long CurrentRodId { get => currentRodId; set { currentRodId = value; OnProgressChanged?.Invoke(); } }
        public int MaxLevel => levelConfig.MaxLevel;
        public bool IsMaxLevel => Level >= MaxLevel;

        int GetProgressValue(PropertyType type)
        {
            PropertyBag property = GameDataManager.Instance.GetProperty(type);
            return property?.Value ?? GameDataManager.Instance.GetPropertyData(type).DeftualNumber;
        }
        #region 鱼竿选择
        /// <summary>
        /// 从背包里实际拥有的鱼竿中选出等级最高的一支并装备。
        /// 鱼竿无独立等级字段，以难度降低值 <see cref="FishRodData.DifficultyReduction"/> 越大视为等级越高
        /// （竹0/玻璃30/铱金50/高级100）。未拥有任何鱼竿时保持当前（默认竹鱼竿）。
        /// </summary>
        public void SelectHighestLevelRod()
        {
            long bestId = -1;
            int bestReduction = int.MinValue;
            foreach (var kv in rodConfig.RodDataDict)
            {
                if (InventoryManager.Instance.GetItemCount(kv.Key) <= 0)
                    continue;
                if (kv.Value.DifficultyReduction > bestReduction)
                {
                    bestReduction = kv.Value.DifficultyReduction;
                    bestId = kv.Key;
                }
            }
            if (bestId >= 0)
                CurrentRodId = bestId;
        }
        #endregion

        #region 难度 / 等级加成
        /// <summary>当前鱼竿对钓鱼难度的降低值。</summary>
        public int RodDifficultyReduction => rodConfig.GetDifficultyReduction(currentRodId);

        /// <summary>实际钓鱼难度 = 鱼本身难度 - 当前鱼竿难度降低（下限 1）。同时作为 QTE 捕获进度的目标点。</summary>
        public int CalcFishDifficulty(int fishDifficulty) => Mathf.Max(1, fishDifficulty - RodDifficultyReduction);

        /// <summary>当前等级的遛鱼绿条加宽(px)（策划案 5.6）。</summary>
        public int GreenBarWidthBonus => levelConfig.Get(Level).GreenBarWidthBonus;

        /// <summary>当前等级的咬钩等待时间减免比例(0~1)（策划案 5.6）。</summary>
        public float BiteTimeReduceFactor => Mathf.Clamp01(levelConfig.Get(Level).BiteTimeReducePercent / 100f);

        /// <summary>指定等级升到下一级所需经验（满级或未配置返回0）。</summary>
        public int GetExpToNext(int forLevel) => levelConfig.Get(forLevel)?.ExpToNext ?? 0;
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
            int level = Level;
            int exp = Exp + gain;

            // 逐级结算升级（每级重新累计）
            while(level < MaxLevel)
            {
                int need = levelConfig.Get(level)?.ExpToNext ?? 0;
                if(need <= 0 || exp < need)
                    break;
                exp -= need;
                level++;
            }
            if(level >= MaxLevel)
                exp = 0;

            GameDataManager.Instance.SetProperty(PropertyType.FishLevel, level);
            GameDataManager.Instance.SetProperty(PropertyType.FishExp, exp);

            OnProgressChanged?.Invoke();
            return gain;
        }
        #endregion
    }
}
