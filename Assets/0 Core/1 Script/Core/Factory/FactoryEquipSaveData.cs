using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 工厂系统存档字段（partial 扩展核心 <see cref="GameSaveData"/>）。
    /// 随存档读写，由常驻管理器 FactoryEquipManager 在 SaveData/LoadData 中填充 / 还原。
    /// </summary>
    public partial class GameSaveData
    {
        [LabelText("工厂升级设备")]
        public FactoryEquipSaveData FactoryEquip = new ();
    }

    /// <summary>工厂「升级设备」存档：记录各设备当前等级。</summary>
    [Serializable]
    public class FactoryEquipSaveData
    {
        /// <summary>各设备等级条目（设备 Id → 当前等级，0=未升级）。</summary>
        public List<FactoryEquipLevelEntry> Levels = new ();
    }

    /// <summary>单台设备的存档条目。</summary>
    [Serializable]
    public class FactoryEquipLevelEntry
    {
        public int Id;
        public int Level;
    }
}
