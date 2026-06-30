using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

/// <summary>
/// 升级设备单个格子（横向条目，见设计图）：左侧名称卡（LV 徽标 + 名称文本）+ 中部描述文本 + 右侧金币价格胶囊（即升级按钮）。
/// 名称 / 描述均为多语言（取自设备 <see cref="FactoryEquipData.NameKey"/>/<see cref="FactoryEquipData.DescKey"/>，文案在 Factory 表 / FactoryUpgradePanel.csv）。
/// 由 <see cref="FactoryUpgradePanel"/> 从隐藏模板实例化并绑定设备数据；点击价格胶囊回调上层执行升级（扣费/升级/存档走 <see cref="FactoryEquipManager"/>）。
/// </summary>
public class FactoryUpgradeCellUI : MonoBehaviour
{
    [SerializeField] TMP_Text nameText;     // 设备名称（多语言）
    [SerializeField] TMP_Text levelText;    // "LVx" 徽标
    [SerializeField] TMP_Text descText;     // 设备描述（多语言）
    [SerializeField] TMP_Text costText;     // "¥{cost}"（满级置空）
    [SerializeField] GameObject maxFlag;    // 满级标识（满级时显示，可选）
    [SerializeField] Button upgradeButton;  // 价格胶囊 = 升级按钮

    FactoryEquipData data;
    Action<FactoryUpgradeCellUI> onUpgrade;

    /// <summary>绑定的设备 Id。</summary>
    public int Id => data.Id;

    void Awake()
    {
        upgradeButton.onClick.AddListener(() => onUpgrade?.Invoke(this));
    }

    /// <summary>用设备配置填充本格；<paramref name="onUpgrade"/> 在点击升级时回调。</summary>
    public void Set(FactoryEquipData data, Action<FactoryUpgradeCellUI> onUpgrade)
    {
        this.data = data;
        this.onUpgrade = onUpgrade;

        nameText.text = Loc(data.NameKey);
        descText.text = Loc(data.DescKey);

        Refresh();
    }

    /// <summary>按当前等级刷新 徽标 / 价格 / 按钮可用态。升级成功后由面板调用。</summary>
    public void Refresh()
    {
        FactoryEquipManager mgr = FactoryEquipManager.Instance;
        int level = mgr.GetLevel(data.Id);
        bool isMax = level >= data.MaxLevel;

        levelText.text = "LV" + level;
        maxFlag.SetActive(isMax);
        costText.text = isMax ? string.Empty : "¥" + mgr.GetNextCost(data.Id);
        upgradeButton.interactable = !isMax;
    }

    // 同步取本地化串（无变量），与 FactoryRecycleCellUI 取价同套路；格子在运行时构建，本地化已就绪
    static string Loc(string key)
    {
        if(string.IsNullOrEmpty(key))
            return string.Empty;
        LocalizedString ls = new ()
        {
            TableReference = LocalizeTableSet.Factory,
            TableEntryReference = key,
        };
        return ls.GetLocalizedString();
    }

#if UNITY_EDITOR
    /// <summary>编辑器一键生成时绑定内部引用（仅供 <see cref="FactoryUpgradePanel"/> 生成器调用）。</summary>
    public void EditorBind(TMP_Text name, TMP_Text level, TMP_Text desc, TMP_Text cost, GameObject maxFlagGo, Button btn)
    {
        nameText = name;
        levelText = level;
        descText = desc;
        costText = cost;
        maxFlag = maxFlagGo;
        upgradeButton = btn;
    }
#endif
}
