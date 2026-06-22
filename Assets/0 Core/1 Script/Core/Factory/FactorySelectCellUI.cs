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
    [SerializeField] Image selectFrame;
    [SerializeField] int index;

    Action<int> onClick;

    public void Bind(int index, Action<int> onClick)
    {
        this.index = index;
        this.onClick = onClick;
    }

    public void SetIcon(string path)
    {
        iconImage.SetIcon(path);
    }
    public void SetName(string nameKey)
    {
        nameLse.SetText(LocalizeTableSet.InventoryItem, nameKey);
    }
    public void SetSub(string text) => subText.text = text;
    public void SetSelected(bool on) => selectFrame.enabled = on;

    public void OnPointerClick(PointerEventData e) => onClick?.Invoke(index);
}
