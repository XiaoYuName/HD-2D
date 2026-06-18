using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 「需要制作的产品种类」弹窗：列出 <see cref="FactoryProductConfig"/> 的全部产品，单选后回调所选产品。
/// 用法：UISystem.Instance.OpenUI&lt;FactoryProductSelectPanel&gt;(id).Show(config, preSelected, onConfirm);
/// </summary>
public class FactoryProductSelectPanel : UIBase
{
    [Title("Ref")]
    [LabelText("格子容器")][SerializeField] RectTransform gridContainer;
    [LabelText("格子模板(隐藏)")][SerializeField] FactorySelectCellUI cellTemplate;
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

    public void Show(FactoryProductConfig config, FactoryProductData preSelected, Action<FactoryProductData> onConfirm)
    {
        this.onConfirm = onConfirm;

        source.Clear();
        source.AddRange(config.DataDict.Values);
        selectedIndex = preSelected != null ? source.IndexOf(preSelected) : -1;

        BuildCells();
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
            cell.SetName(LanguageManager.Instance.GetLocalizedString(LocalizeTableSet.Factory, product.NameKey));
            cell.SetSub(GetPriceText(product.UnitPrice));
            cell.SetSelected(i == selectedIndex);
            cell.Bind(i, OnCellClick);
            cells.Add(cell);
        }
    }

    void OnCellClick(int index)
    {
        if(selectedIndex >= 0 && selectedIndex < cells.Count)
            cells[selectedIndex].SetSelected(false);
        selectedIndex = index;
        cells[index].SetSelected(true);
    }

    void OnConfirmButton()
    {
        if(selectedIndex < 0)
            return;
        onConfirm?.Invoke(source[selectedIndex]);
        Close();
    }

    void OnCloseButton() => Close();

    // 单价含占位符，单独构造 LocalizedString 灌入 {Price} 后取当前语言成品串
    static string GetPriceText(int price)
    {
        LocalizedString ls = new () { TableReference = LocalizeTableSet.Factory, TableEntryReference = FactoryLocKeySet.UnitPriceFmt };
        ls.SetVar(LocalizeVarSet.FactoryMain.Price, price, false);
        return ls.GetLocalizedString();
    }
}
