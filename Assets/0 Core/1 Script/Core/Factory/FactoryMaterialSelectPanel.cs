using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 「添加素材」弹窗：列出玩家背包物品（可按类型过滤），多选后回调选中列表。素材数据复用物品系统（<see cref="PlayerBag"/>）。
/// 用法：UISystem.Instance.OpenUI&lt;FactoryMaterialSelectPanel&gt;(id).Show(type, preSelected, onConfirm);
/// </summary>
public class FactoryMaterialSelectPanel : UIBase
{
    [Title("Ref")]
    [LabelText("格子容器")][SerializeField] RectTransform gridContainer;
    [LabelText("格子模板(隐藏)")][SerializeField] FactorySelectCellUI cellTemplate;
    [Title("Button")]
    [LabelText("确定选择")][SerializeField] Button confirmButton;
    [LabelText("关闭")][SerializeField] Button closeButton;

    readonly List<FactorySelectCellUI> cells = new ();
    readonly List<ItemInfo> source = new ();
    readonly HashSet<int> selectedIndices = new ();
    Action<List<ItemInfo>> onConfirm;

    public override void Init()
    {
        confirmButton.onClick.AddListener(OnConfirmButton);
        closeButton.onClick.AddListener(OnCloseButton);
        cellTemplate.gameObject.SetActive(false);
    }

    /// <summary>展示背包物品供多选。<paramref name="filter"/> 为 None 时列出全部物品。</summary>
    public void Show(ItemType filter, IEnumerable<ItemInfo> preSelected, Action<List<ItemInfo>> onConfirm)
    {
        this.onConfirm = onConfirm;

        source.Clear();
        source.AddRange(filter == ItemType.None ? PlayerInfo.St.Bag.ItemList : PlayerInfo.St.Bag.GetItemList(filter));

        selectedIndices.Clear();
        if(preSelected != null)
            foreach(ItemInfo info in preSelected)
            {
                int idx = source.IndexOf(info);
                if(idx >= 0)
                    selectedIndices.Add(idx);
            }

        BuildCells();
    }

    void BuildCells()
    {
        for(int i = 0; i < cells.Count; i++)
            Destroy(cells[i].gameObject);
        cells.Clear();

        for(int i = 0; i < source.Count; i++)
        {
            ItemInfo info = source[i];
            FactorySelectCellUI cell = Instantiate(cellTemplate, gridContainer);
            cell.gameObject.SetActive(true);
            cell.SetIcon(info.IconPath);
            cell.SetName(info.Name);
            cell.SetSub("x" + info.Count);
            cell.SetSelected(selectedIndices.Contains(i));
            cell.Bind(i, OnCellClick);
            cells.Add(cell);
        }
    }

    void OnCellClick(int index)
    {
        if(!selectedIndices.Remove(index))
            selectedIndices.Add(index);
        cells[index].SetSelected(selectedIndices.Contains(index));
    }

    void OnConfirmButton()
    {
        List<ItemInfo> result = new ();
        foreach(int idx in selectedIndices)
            result.Add(source[idx]);

        onConfirm?.Invoke(result);
        Close();
    }

    void OnCloseButton() => Close();
}
