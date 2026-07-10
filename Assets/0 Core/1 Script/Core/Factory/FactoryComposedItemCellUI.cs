using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 展示一件「框架+贴纸」组合物(<see cref="FactoryComposedItemInfo"/>：生产资料或周边商品)的格子：
/// 图标(三层合成) + 名称 + 数量 + 单价，另加可选的选中高亮框 + 点击回调，供选择类面板使用
/// （<see cref="FactoryProductSelectPanel"/> 选生产资料 / <see cref="FactorySettlePanel"/> 展示结算产出）。
/// 展示部分模仿 <see cref="FactoryMoldMgLeftUpItemUI"/>，按用户要求另建此类，不改动原脚本。
/// </summary>
public class FactoryComposedItemCellUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] FactoryMoldItemIcon iconImage;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text countText;
    [SerializeField] LocTextVar priceText;
    [SerializeField] Image selectFrame;   // 仅选择面板场景需要，纯展示场景可不赋值

    int index;
    Action<int> onClick;

    public void Set(FactoryComposedItemInfo itemInfo)
    {
        if(itemInfo == null)
        {
            iconImage.gameObject.SetActive(false);
            countText.text = "";
            nameText.text = "";
            priceText.Clear();
            return;
        }

        iconImage.gameObject.SetActive(true);
        iconImage.Set(itemInfo);
        countText.text = "x" + itemInfo.Count.ToString();
        nameText.text = itemInfo.GetName();
        priceText.SetVar(LocVarSet.FactoryMain.Price, itemInfo.GetValue());
    }

    public void Set(int index, Action<int> onClick)
    {
        this.index = index;
        this.onClick = onClick;
    }

    /// <summary>选中高亮开关；未指定 selectFrame 时安全跳过（纯展示场景不需要）。</summary>
    public void SetSelected(bool on)
    {
        selectFrame.enabled = on; 
    }
    public void OnPointerClick(PointerEventData e) => onClick?.Invoke(index);
}
