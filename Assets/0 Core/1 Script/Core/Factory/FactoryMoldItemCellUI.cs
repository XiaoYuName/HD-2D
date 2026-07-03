using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// 「物料制作」面板（<see cref="FactoryMoldMgPanel"/>）左侧物品列表的格子：图标 + 名称 + 数量 + 选中描边。
/// 由面板从隐藏模板实例化并按下标回调。物品名称为多语言 Key，走 <see cref="LocalizeTableSet.InventoryItem"/> 表。
/// 独立于通用的 FactorySelectCellUI，便于物料面板后续按需扩展（如分别标注框架 / 贴纸）。
/// </summary>
public class FactoryMoldItemCellUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Image iconImage;
    [SerializeField] LocalizeStringEvent nameLse;
    [SerializeField] TMP_Text countText;
    [SerializeField] Image selectFrame, nameBg;
    [SerializeField] Color seColor, unSeColor;

    int index;
    Action<int> onClick;

    /// <summary>填充一个格子：下标、图标 AA Key、名称多语言 Key、数量、是否选中、点击回调。</summary>
    public void Set(int index, string iconPath, string nameKey, int count, bool selected, Action<int> onClick)
    {
        this.index = index;
        this.onClick = onClick;
        iconImage.SetIcon(iconPath);
        nameLse.SetText(LocalizeTableSet.InventoryItem, nameKey);
        countText.text = "x" + count;

        SetSelected(selected);
    }

    public void SetSelected(bool on)
    {
        selectFrame.enabled = on;

        if (on)
            nameBg.color = seColor;
        else
            nameBg.color = unSeColor;
    }

    public void OnPointerClick(PointerEventData e) => onClick?.Invoke(index);
}
