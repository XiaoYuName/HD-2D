using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 「需要制作的产品种类」弹窗：列出主面板传入的手办产品（来自 <see cref="ItemConfig"/> 的 Figure 物品），单选后回调所选产品；
/// 下方展示所选产品的名称与介绍。产品名称 / 描述均为 <see cref="LocTableSet.InventoryItem"/> 表的多语言 Key。
/// 用法：UISystem.Instance.OpenUI&lt;FactoryProductSelectPanel&gt;(id).Show(products, preSelected, onConfirm);
/// </summary>
public class FactoryProductSelectPanel : UIBase
{
    [Title("Ref")]
    [LabelText("格子容器")][SerializeField] RectTransform gridContainer;
    [LabelText("格子模板(隐藏)")][SerializeField] FactorySelectCellUI cellTemplate;
    [LabelText("产品名标题")][SerializeField] TMP_Text descTitleText;
    [LabelText("产品介绍正文")][SerializeField] TMP_Text descBodyText;
    [Title("Button")]
    [LabelText("确定选择")][SerializeField] Button confirmButton;
    [LabelText("关闭")][SerializeField] Button closeButton;

    readonly List<FactorySelectCellUI> cells = new ();
    readonly List<FactoryProductData> source = new ();
    int selectedIndex = -1;
    Action<FactoryProductData> onConfirm;

    public override void Init()
    {
        confirmButton.onClick.AddListener(OnConfirmButton);
        closeButton.onClick.AddListener(OnCloseButton);
        cellTemplate.gameObject.SetActive(false);
    }

    public void Show(IReadOnlyList<FactoryProductData> products, FactoryProductData preSelected, Action<FactoryProductData> onConfirm)
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
            FactoryProductData product = source[i];
            FactorySelectCellUI cell = Instantiate(cellTemplate, gridContainer);
            cell.gameObject.SetActive(true);
            cell.SetIcon(product.IconPath);
            cell.SetName(product.NameKey);   // SetName(string) 默认走 InventoryItem 表，正是物品名所在表
            cell.SetSub(GetPriceText(product.UnitPrice));
            cell.SetSelected(i == selectedIndex);
            cell.Set(i, OnCellClick);
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

    // 刷新下方所选产品的名称与介绍（一次性取当前语言文案，空选则清空）
    void RefreshDesc(FactoryProductData p)
    {
        descTitleText.text = p != null ? L(p.NameKey) : string.Empty;
        descBodyText.text = p != null ? L(p.DescKey) : string.Empty;
    }

    void OnConfirmButton()
    {
        if(selectedIndex < 0)
            return;
        onConfirm?.Invoke(source[selectedIndex]);
        Close();
    }

    void OnCloseButton() => Close();

    // 产品名称 / 描述为物品多语言 Key，落在 InventoryItem 表（单价格式串才在 Factory 表，见 GetPriceText）
    static string L(string key) => LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, key);

    // 单价含占位符，单独构造 LocalizedString 灌入 {Price} 后取当前语言成品串
    static string GetPriceText(int price)
    {
        LocalizedString ls = new () { TableReference = LocTableSet.Factory, TableEntryReference = FactoryLocKeySet.UnitPriceFmt };
        ls.SetVar(LocVarSet.FactoryMain.Price, price, false);
        return ls.GetLocalizedString();
    }
}