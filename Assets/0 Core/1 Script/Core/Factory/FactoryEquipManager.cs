using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 工厂「升级设备」运行时管理器：持有设备配置（<see cref="FactoryEquipmentConfig"/>）与各设备当前等级，
/// 提供 取等级 / 取加成 / 取下一级费用 / 升级 等接口。等级随存档读写（字段见 <see cref="GameSaveData.FactoryEquip"/>）。
/// 实现 <see cref="ISaveable"/>，须与 <see cref="GameDataManager"/> 一样常驻（放在启动/常驻场景），
/// 在 <c>Start</c> 中注册到存档系统，确保读档前已注册、能收到 LoadData。
/// </summary>
public class FactoryEquipManager : MonoBehaviour, ISaveable
{
    public static FactoryEquipManager St => st != null ? st : st = FindAnyObjectByType<FactoryEquipManager>();
    static FactoryEquipManager st;
    [LabelText("升级设备配置")][SerializeField] FactoryEquipmentConfig config;

    public FactoryEquipmentConfig Config => config;

    // 设备 Id -> 当前等级（0=未升级）。运行时唯一数据源，存档时落入 GameSaveData.FactoryEquip
    [ShowInInspector] readonly Dictionary<int, int> levels = new ();

    /// <summary>任一设备等级变化（参数为设备 Id），UI 据此局部刷新。</summary>
    public event Action<int> OnEquipChanged;

    #region ISaveable
    public string GUID => "FactoryEquipManager";

    void Awake()
    {
        st = this;
    }
    void OnDestroy()
    {
        if(st == this)
            st = null;
    }
    void Start()
    {
        SaveGameManager.Instance.RegisterSaveable(this);
    }
    void ODestroy()
    {
        SaveGameManager.Instance.RemoveSaveable(this);
    }
    public void SaveData(GameSaveData data)
    {
        data.FactoryEquip.Levels.Clear();
        foreach(KeyValuePair<int, int> kv in levels)
            data.FactoryEquip.Levels.Add(new FactoryEquipLevelEntry { Id = kv.Key, Level = kv.Value });
    }

    public void LoadData(GameSaveData data)
    {
        levels.Clear();
        foreach(FactoryEquipLevelEntry e in data.FactoryEquip.Levels)
            levels[e.Id] = e.Level;
    }
    #endregion

    #region 查询
    /// <summary>设备配置数据；取不到返回 null。</summary>
    public FactoryEquipData GetData(int id) => config.DataDict.TryGetValue(id, out FactoryEquipData d) ? d : null;

    /// <summary>设备当前等级；未升级过的设备返回初始等级 <see cref="FactoryEquipData.BaseLevel"/>。</summary>
    public int GetLevel(int id) => levels.TryGetValue(id, out int lv) ? lv : FactoryEquipData.BaseLevel;

    /// <summary>是否已满级。</summary>
    public bool IsMax(int id)
    {
        FactoryEquipData d = GetData(id);
        return d != null && GetLevel(id) >= d.MaxLevel;
    }

    /// <summary>当前等级的加成值。</summary>
    public int GetCurrentBonus(int id)
    {
        FactoryEquipData d = GetData(id);
        return d != null ? d.GetBonus(GetLevel(id)) : 0;
    }

    /// <summary>升到下一级的费用（金币）；已满级 / 无配置返回 0。</summary>
    public int GetNextCost(int id)
    {
        FactoryEquipData d = GetData(id);
        if(d == null)
            return 0;
        int lv = GetLevel(id);
        return lv < d.MaxLevel ? d.GetUpgradeCost(lv) : 0;
    }

    /// <summary>
    /// 汇总某一加成类型下全部设备的当前加成值（按各自当前等级）。
    /// 与 <see cref="FactoryGameConfig"/> 的基础值叠加得最终数值：
    /// 总良品率% = BaseYieldRate + SumBonus(Yield)；总生产量 = BaseProductionVolume + SumBonus(ProductionVolume)。
    /// </summary>
    public int SumBonus(FactoryEquipBonusType type)
    {
        if(config == null || config.DataDict == null)
            return 0;
        int sum = 0;
        foreach(FactoryEquipData d in config.DataDict.Values)
            if(d.BonusType == type)
                sum += d.GetBonus(GetLevel(d.Id));
        return sum;
    }
    #endregion

    #region 升级
    /// <summary>
    /// 尝试升级一台设备：扣金币、等级 +1、存档并回调。
    /// 满级 / 金币不足 / 无配置均失败返回 false（不弹提示，提示由调用方据 <see cref="IsMax"/>/金币判断处理）。
    /// </summary>
    public bool TryUpgrade(int id)
    {
        FactoryEquipData d = GetData(id);
        if(d == null)
            return false;

        int lv = GetLevel(id);
        if(lv >= d.MaxLevel)
            return false;   // 已满级

        int cost = d.GetUpgradeCost(lv);
        PlayerBag bag = PlayerInfo.St != null ? PlayerInfo.St.Bag : null;
        if(bag == null || !bag.HasMoney(cost))
            return false;   // 金币不足

        bag.SubMoney(cost);
        levels[id] = lv + 1;
        SaveGameManager.Instance.Save();
        OnEquipChanged?.Invoke(id);
        return true;
    }
    #endregion
}
