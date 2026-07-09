using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 「回收站」标签内容（挂在 <see cref="FactoryMainPanel"/> 的 recycleContent 上）：列出背包里可回收的周边物品（默认 <see cref="ItemType.Merchandise"/>），
/// 每格可加减选择回收数量（封顶持有量），可上下滑动；底部汇总预期收入并一键回收出售（扣除背包物品、按单价折算为金币）。
/// 单件回收价取物品配置的售价（<see cref="ItemData.SellAmount"/>）。由主面板在切到本标签时调用 <see cref="Refresh"/>。
/// </summary>
public class FactoryRecyclePanel : MonoBehaviour
{
    [Title("Rule")]
    static readonly List<ItemType> recyclableTypes = new () { ItemType.Merchandise };

    [Title("Ref")]
    [LabelText("格子容器(滚动内容)")][SerializeField] RectTransform gridContainer;
    [LabelText("格子模板(隐藏，运行时克隆)")][SerializeField] FactoryRecycleCellUI cellTemplate;
    [LabelText("预期收入数值文本(¥N)")][SerializeField] TMP_Text incomeValueText;
    [LabelText("空列表提示")][SerializeField] LocalizeStringEvent emptyText;
    [Title("Button")]
    [LabelText("回收出售")][SerializeField] Button sellButton;
    [LabelText("提示气泡")][SerializeField] WarnTip warnTip;

    readonly List<FactoryRecycleCellUI> cells = new ();
    bool inited;

    void Awake() => EnsureInit();

    void EnsureInit()
    {
        if(inited)
            return;
        inited = true;
        sellButton.onClick.AddListener(OnSellButton); 
        cellTemplate.gameObject.SetActive(false);
    }

    /// <summary>重建回收列表并清空已选；主面板每次切到回收站标签时调用。</summary>
    public void Refresh()
    {
        EnsureInit();
        BuildCells();
        RefreshIncome();
    }

    void BuildCells()
    {
        for(int i = cells.Count - 1; i >= 0; i--)
            Destroy(cells[i].gameObject);
        cells.Clear();

        InventoryManager bag = InventoryManager.Instance;
        foreach(ItemType type in recyclableTypes)
            foreach(ItemInfo info in bag.GetItemList(type))
            {
                FactoryRecycleCellUI cell = Instantiate(cellTemplate, gridContainer);
                cell.gameObject.SetActive(true);
                cell.Set(info,  ItemManager.St.GetItemData(info.Id).SellAmount, RefreshIncome);
                cells.Add(cell);
            }
        emptyText.gameObject.SetActive(cells.Count == 0); 
    }
    void RefreshIncome()
    {
        int total = 0;
        foreach(FactoryRecycleCellUI cell in cells)
            total += cell.Income;
        incomeValueText.text = "¥" + total; 
    }

    // 回收出售：把各格已选数量从背包扣除并按预期收入折算为金币；未选任何周边时提示
    void OnSellButton()
    {
        int income = 0;
        List<FactoryRecycleCellUI> picked = new ();
        foreach(FactoryRecycleCellUI cell in cells)
            if(cell.Selected > 0)
            {
                income += cell.Income;
                picked.Add(cell);
            }

        if(picked.Count == 0)
        {
            warnTip.ShowTip(LocTableSet.Factory, FactoryLocKeySet.Recycle.NothingSelected);
            return;
        }

        InventoryManager bag = InventoryManager.Instance;
        foreach(FactoryRecycleCellUI cell in picked)
            bag.ConsumeItem(cell.Info, cell.Selected);
        if(income > 0)
            bag.AddMoney(income);

        warnTip.ShowTip(LocTableSet.Factory, FactoryLocKeySet.Recycle.Sold);

        Refresh();   // 数量已变，重建列表并清空已选
    }
}
