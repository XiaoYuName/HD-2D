using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 「开始加工」选生产资料(模具)弹窗：列出主面板传入的背包生产资料（运行时自描述物品 <see cref="FactoryMoldItemInfo"/>），
/// 单选后回调所选资料；下方展示所选资料的名称与介绍（<see cref="FactoryMoldItemInfo"/> 已自带解析好的当前语言文案）。
/// 用法：UISystem.Instance.OpenUI&lt;FactoryProductSelectPanel&gt;(id).Show(products, preSelected, onConfirm);
/// </summary>
public class FactoryProductSelectPanel : UIBase
{
    [Title("Ref")]
    [LabelText("格子容器")][SerializeField] RectTransform gridContainer;
    [LabelText("格子模板(隐藏)")][SerializeField] FactoryComposedItemCellUI cellTemplate;
    [LabelText("产品名标题")][SerializeField] TMP_Text descTitleText;
    [LabelText("产品介绍正文")][SerializeField] TMP_Text descBodyText;
    [Title("Button")]
    [LabelText("确定选择")][SerializeField] Button confirmButton;
    [LabelText("关闭")][SerializeField] Button closeButton;

    readonly List<FactoryComposedItemCellUI> cells = new ();
    readonly List<FactoryMoldItemInfo> source = new ();
    int selectedIndex = -1;
    Action<FactoryMoldItemInfo> onConfirm;

    public override void Init()
    {
        confirmButton.onClick.AddListener(OnConfirmButton);
        closeButton.onClick.AddListener(OnCloseButton);
        cellTemplate.gameObject.SetActive(false);
    }

    public void Show(IReadOnlyList<FactoryMoldItemInfo> products, FactoryMoldItemInfo preSelected, Action<FactoryMoldItemInfo> onConfirm)
    {
        this.onConfirm = onConfirm;

        source.Clear();
        if(products != null)
            source.AddRange(products);
        selectedIndex = preSelected != null ? source.IndexOf(preSelected) : -1;

        BuildCells();
        RefreshDesc(selectedIndex >= 0 ? source[selectedIndex] : null);
    }

    void BuildCells()
    {
        for(int i = 0; i < cells.Count; i++)
            Destroy(cells[i].gameObject);
        cells.Clear();

        for(int i = 0; i < source.Count; i++)
        {
            FactoryMoldItemInfo product = source[i];
            FactoryComposedItemCellUI cell = Instantiate(cellTemplate, gridContainer);
            cell.gameObject.SetActive(true);
            cell.Set(product);   // 图标(三层合成)/名称/数量/单价均由物品自身携带
            cell.SetSelected(i == selectedIndex);
            cell.SetIndex(i, OnCellClick);
            cells.Add(cell);
        }
    }

    void OnCellClick(int index)
    {
        if(selectedIndex >= 0 && selectedIndex < cells.Count)
            cells[selectedIndex].SetSelected(false);
        selectedIndex = index;
        cells[index].SetSelected(true);
        RefreshDesc(source[index]);
    }

    // 刷新下方所选资料的名称与介绍（FactoryMoldItemInfo.Name/Desc 已是当前语言解析好的文案，无需再查多语言表）
    void RefreshDesc(FactoryMoldItemInfo p)
    {
        descTitleText.text = p != null ? p.Name : string.Empty;
        descBodyText.text = p != null ? p.Desc : string.Empty;
    }

    void OnConfirmButton()
    {
        if(selectedIndex < 0)
            return;
        onConfirm?.Invoke(source[selectedIndex]);
        Close();
    }

    void OnCloseButton() => Close();
}