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
/// 「需要制作的产品种类」弹窗：列出 <see cref="FactoryProductConfig"/> 的全部产品，单选后回调所选产品；
/// 下方展示所选产品的名称与介绍。
/// 用法：UISystem.Instance.OpenUI&lt;FactoryProductSelectPanel&gt;(id).Show(config, preSelected, onConfirm);
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

    public void Show(FactoryProductConfig config, FactoryProductData preSelected, Action<FactoryProductData> onConfirm)
    {
        this.onConfirm = onConfirm;

        source.Clear();
        source.AddRange(config.DataDict.Values);
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
            cell.SetName(L(product.NameKey));
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

    static string L(string key) => LanguageManager.Instance.GetLocalizedString(LocalizeTableSet.Factory, key);

    // 单价含占位符，单独构造 LocalizedString 灌入 {Price} 后取当前语言成品串
    static string GetPriceText(int price)
    {
        LocalizedString ls = new () { TableReference = LocalizeTableSet.Factory, TableEntryReference = FactoryLocKeySet.UnitPriceFmt };
        ls.SetVar(LocalizeVarSet.FactoryMain.Price, price, false);
        return ls.GetLocalizedString();
    }

#if UNITY_EDITOR
    #region 一键生成界面（仅编辑器）
    [PropertySpace(8)]
    [Button("创建界面 UI", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("生成「产品种类」弹窗：标题、产品网格（单价标签+名称）、所选产品介绍区、确定/关闭按钮，并生成可选格子模板（橙色选中描边）。可重复点击，旧生成内容会先清空。", InfoMessageType.Info)]
    void BuildUI()
    {
        for(int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        Image window = FactoryUIGen.Img("Window", transform, new Color(0.93f, 0.90f, 0.82f, 0.98f));
        FactoryUIGen.Center(window.rectTransform, 840, 640, 0, 0);

        FactoryUIGen.Loc("Title", window.transform, FactoryLocKeySet.Select.SelectProductTitle, 30, new Color(0.35f, 0.3f, 0.2f), TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().anchoredPosition = new Vector2(-370, 280);

        closeButton = FactoryUIGen.Btn("CloseButton", window.transform, FactoryLocKeySet.Back, new Color(0.9f, 0.87f, 0.78f), new Color(0.4f, 0.35f, 0.25f));
        FactoryUIGen.Anchor((RectTransform)closeButton.transform, new Vector2(1, 1), new Vector2(1, 1), 56, 56, -8, -8);

        // 产品网格（单行）
        RectTransform grid = FactoryUIGen.Node("Grid", window.transform);
        FactoryUIGen.Center(grid, 760, 200, 0, 150);
        GridLayoutGroup glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(132, 170);
        glg.spacing = new Vector2(16, 16);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 5;
        glg.childAlignment = TextAnchor.MiddleCenter;
        gridContainer = grid;

        cellTemplate = MakeCellTemplate(grid);
        FactoryUIGen.WrapInScrollView(grid);   // 产品多时可上下滑动

        // 所选产品介绍区
        descTitleText = FactoryUIGen.Text("DescTitle", window.transform, string.Empty, 30, new Color(0.25f, 0.22f, 0.16f), TextAlignmentOptions.Left);
        descTitleText.fontStyle = FontStyles.Bold;
        FactoryUIGen.Center(descTitleText.rectTransform, 760, 40, 0, 0);

        Image divider = FactoryUIGen.Img("Divider", window.transform, new Color(0.5f, 0.45f, 0.35f, 0.5f));
        FactoryUIGen.Center(divider.rectTransform, 760, 2, 0, -24);

        descBodyText = FactoryUIGen.Text("DescBody", window.transform, string.Empty, 20, new Color(0.4f, 0.36f, 0.3f), TextAlignmentOptions.TopLeft);
        descBodyText.textWrappingMode = TextWrappingModes.Normal;
        FactoryUIGen.Center(descBodyText.rectTransform, 760, 150, 0, -110);

        confirmButton = FactoryUIGen.Btn("ConfirmButton", window.transform, FactoryLocKeySet.ConfirmSelect, new Color(0.95f, 0.75f, 0.3f), Color.white);
        FactoryUIGen.Center((RectTransform)confirmButton.transform, 200, 64, 0, -275);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryProductSelectPanel] 界面已生成", this);
    }

    // 产品格子模板：选中描边(橙) + 图标 + 单价(图标底部) + 名称(下方)，挂 FactorySelectCellUI 并赋引用，默认隐藏
    FactorySelectCellUI MakeCellTemplate(Transform parent)
    {
        Image body = FactoryUIGen.Img("CellTemplate", parent, new Color(1f, 1f, 1f, 0f));

        Image frame = FactoryUIGen.Img("SelectFrame", body.transform, new Color(0.95f, 0.7f, 0.2f, 1f));
        FactoryUIGen.Center(frame.rectTransform, 108, 108, 0, 28);
        frame.enabled = false;

        Image icon = FactoryUIGen.Img("Icon", body.transform, Color.white);
        FactoryUIGen.Center(icon.rectTransform, 96, 96, 0, 28);

        // 单价标签：贴图标底部
        TMP_Text price = FactoryUIGen.Text("Price", body.transform, string.Empty, 20, new Color(0.55f, 0.35f, 0.1f), TextAlignmentOptions.Center);
        FactoryUIGen.Center(price.rectTransform, 96, 26, 0, -8);

        TMP_Text name = FactoryUIGen.Text("Name", body.transform, string.Empty, 22, new Color(0.3f, 0.28f, 0.22f), TextAlignmentOptions.Center);
        FactoryUIGen.Center(name.rectTransform, 130, 28, 0, -60);

        FactorySelectCellUI cell = body.gameObject.AddComponent<FactorySelectCellUI>();
        cell.iconImage = icon;
        cell.nameText = name;
        cell.subText = price;
        cell.selectFrame = frame;
        body.gameObject.SetActive(false);
        return cell;
    }
    #endregion
#endif
}
