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
    [Title("规则")]
    [LabelText("最多可选数量(暂限2，可扩展)"), MinValue(1)][SerializeField] int maxSelectCount = 2;

    [SerializeField] readonly List<FactorySelectCellUI> cells = new ();
    [SerializeField] readonly List<ItemInfo> source = new ();
    readonly HashSet<int> selectedIndices = new ();
    Action<List<ItemInfo>> onConfirm;

    public override void Init()
    {
        confirmButton.onClick.AddListener(OnConfirmButton);
        closeButton.onClick.AddListener(OnCloseButton);
        cellTemplate.gameObject.SetActive(false);
    }

    /// <summary>展示背包物品供多选。<paramref name="filters"/> 为空时列出全部物品，否则列出这些类型的并集（如手办模型 + 绘画）。</summary>
    public void Show(IReadOnlyList<ItemType> filters, IEnumerable<ItemInfo> preSelected, Action<List<ItemInfo>> onConfirm)
    {
        this.onConfirm = onConfirm;

        source.Clear();
        foreach(ItemType type in filters)
        source.AddRange(PlayerInfo.St.Bag.GetItemList(type));

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
        bool selected = selectedIndices.Contains(index);
        if(!selected && selectedIndices.Count >= maxSelectCount)
            return;   // 已达可选上限

        if(selected)
            selectedIndices.Remove(index);
        else
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

#if UNITY_EDITOR
    #region 一键生成界面（仅编辑器）
    [PropertySpace(8)]
    [Button("创建界面 UI", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("生成「添加素材」弹窗：标题、物品网格（2×5）、确定/关闭按钮，并生成可选格子模板（青色选中描边）。可重复点击，旧生成内容会先清空。", InfoMessageType.Info)]
    void BuildUI()
    {
        for(int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        Image window = FactoryUIGen.Img("Window", transform, new Color(0.80f, 0.88f, 0.95f, 0.98f));
        FactoryUIGen.Center(window.rectTransform, 780, 470, 0, 0);

        FactoryUIGen.Loc("Title", window.transform, FactoryLocKeySet.Select.AddMaterialTitle, 30, new Color(0.25f, 0.3f, 0.38f), TMPro.TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().anchoredPosition = new Vector2(-340, 195);

        closeButton = FactoryUIGen.Btn("CloseButton", window.transform, FactoryLocKeySet.Back, new Color(0.85f, 0.9f, 0.95f), new Color(0.3f, 0.35f, 0.4f));
        FactoryUIGen.Anchor((RectTransform)closeButton.transform, new Vector2(1, 1), new Vector2(1, 1), 56, 56, -8, -8);

        RectTransform grid = FactoryUIGen.Node("Grid", window.transform);
        FactoryUIGen.Center(grid, 700, 330, 0, 20);
        GridLayoutGroup glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(122, 160);
        glg.spacing = new Vector2(16, 6);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 5;
        glg.childAlignment = TextAnchor.MiddleCenter;
        gridContainer = grid;

        FactoryUIGen.WrapInScrollView(grid);   // 元素多时可上下滑动

        confirmButton = FactoryUIGen.Btn("ConfirmButton", window.transform, FactoryLocKeySet.ConfirmSelect, new Color(0.6f, 0.82f, 0.95f), Color.white);
        FactoryUIGen.Center((RectTransform)confirmButton.transform, 200, 62, 0, -195);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMaterialSelectPanel] 界面已生成", this);
    }

    [PropertySpace(4)]
    [Button("升级元素列表为可滚动(SV)"), GUIColor(0.6f, 0.9f, 0.7f)]
    [InfoBox("把现有「格子容器」就地包进垂直 ScrollRect（右侧滑条、上下滑动），不清空已搭好的界面。已是滚动列表则跳过。", InfoMessageType.Info)]
    void WrapGridInScrollView()
    {
        if(gridContainer == null)
        {
            Debug.LogError("[FactoryMaterialSelectPanel] 请先设置「格子容器(gridContainer)」", this);
            return;
        }
        ScrollRect sr = FactoryUIGen.WrapInScrollView(gridContainer);
        EditorUtility.SetDirty(this);
        Debug.Log($"[FactoryMaterialSelectPanel] 元素列表已可上下滑动：{sr.name}", this);
    }
    #endregion
#endif
}
