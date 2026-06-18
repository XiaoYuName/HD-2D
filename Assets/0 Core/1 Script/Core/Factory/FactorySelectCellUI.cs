using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 工厂选择类面板（添加素材 / 选产品种类）共用的可选格子：图标 + 名称 + 副文本（数量 / 单价）+ 选中高亮框。
/// 由面板从模板实例化并按下标回调，引用在生成界面时直接赋值。
/// </summary>
public class FactorySelectCellUI : MonoBehaviour, IPointerClickHandler
{
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text subText;
    public Image selectFrame;

    int index;
    Action<int> onClick;

    public void Bind(int index, Action<int> onClick)
    {
        this.index = index;
        this.onClick = onClick;
    }

    public void SetIcon(string path) => iconImage.SetIcon(path);
    public void SetName(string text) => nameText.text = text;
    public void SetSub(string text) => subText.text = text;
    public void SetSelected(bool on) => selectFrame.enabled = on;

    public void OnPointerClick(PointerEventData e) => onClick?.Invoke(index);
}
