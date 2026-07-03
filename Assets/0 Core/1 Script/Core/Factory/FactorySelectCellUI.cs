using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Localization.Events;
using UnityEngine.Localization.Components;

/// <summary>
/// 工厂选择类面板（添加素材 / 选产品种类）共用的可选格子：图标 + 名称 + 副文本（数量 / 单价）+ 选中高亮框。
/// 由面板从模板实例化并按下标回调，引用在生成界面时直接赋值。
/// </summary>
public class FactorySelectCellUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Image iconImage;
    [SerializeField] TMP_Text nameText;
    [SerializeField] LocalizeStringEvent nameLse;
    [SerializeField] TMP_Text subText;
    [SerializeField] TMP_Text countText;
    [SerializeField] Image selectFrame;
    [SerializeField] int index;

    Action<int> onClick;

    public void Set(int index, Action<int> onClick)
    {
        this.index = index;
        this.onClick = onClick;
    }

    public void SetIcon(string path)
    {
        iconImage.SetIcon(path);
    }
    // 按物品设置图标：优先用物品自带运行时贴图（如工厂拍照合成图），否则回退按 IconPath 走 AA 加载
    public void SetIcon(ItemInfo info)
    {
        if(info == null)
            return;

        iconImage.SetIcon(info.IconPath);   
    }
    public void SetName(string nameKey)
    {
        nameLse.SetText(LocalizeTableSet.InventoryItem, nameKey);
    }
    // 指定多语言表的取名（产品名在 Factory 表，物品名在 InventoryItem 表）
    public void SetName(string table, string nameKey)
    {
        nameLse.SetText(table, nameKey);
    }
    public void SetSub(string text) => subText.text = text;
    // 数量角标（如 "x999"）；模板未挂数量文本时安全跳过，结算面板专用，选择面板不调用
    public void SetCount(string text)
    {
        countText.text = text;
    }
    public void SetSelected(bool on) => selectFrame.enabled = on;

    public void OnPointerClick(PointerEventData e) => onClick?.Invoke(index);
}
