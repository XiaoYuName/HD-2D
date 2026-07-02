using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 「加工厂」单张制作任务卡：①选择素材（最多 2 个，槽位数跟随已选数量）→ ②选择产品 → 贡献单卡花费。
/// 由 <see cref="FactoryMainPanel"/> 从隐藏模板 Instantiate 出来后调用 <see cref="Set"/> 初始化；
/// 卡内素材 / 产品变化时通过 onChanged 回调通知主面板刷新总金额。
/// 素材数据复用物品系统（<see cref="PlayerBag"/>），产品种类取 <see cref="ItemConfig"/> 中的手办物品（由主面板构建后注入）。
/// 接线：素材槽位 materialSlots / 图标 materialSlotIcons / 添加按钮 addMaterialButton 建议同父级（materialSlotContainer），以便整组水平居中。
/// </summary>
public class FactoryTaskCard : MonoBehaviour
{
    [Title("制作任务卡 - 素材")]
    [LabelText("添加素材按钮(+)")][SerializeField] Button addMaterialButton;
    [LabelText("素材槽位(首个常显，按已选数量依次显示)")][SerializeField] List<Button> materialSlots = new ();
    [LabelText("素材槽图标(与槽位一一对应)")][SerializeField] List<Image> materialSlotIcons = new ();
    [LabelText("素材槽容器(居中排列)")][SerializeField] RectTransform materialSlotContainer;
    [LabelText("素材槽间距")][SerializeField] float materialSlotSpacing = 24f;

    [Title("制作任务卡 - 产品")]
    [LabelText("产品空槽按钮")][SerializeField] Button productSlotButton;
    [LabelText("产品物体按钮")][SerializeField] Button productItemButton;
    [LabelText("产品物体")][SerializeField] GameObject productGo;
    [LabelText("产品图标")][SerializeField] Image productIcon;
    [LabelText("产品单价文本")][SerializeField] LocalizeStringEvent productPriceText;
    [LabelText("产品数量文本")][SerializeField] LocalizeStringEvent productCountText;

    IReadOnlyList<FactoryProductData> products;
    IReadOnlyList<ItemType> materialItemTypes;
    Action onChanged;

    readonly List<ItemInfo> curMaterials = new ();
    FactoryProductData curProduct;
    int visibleMaterialSlots = 1;
    bool bound;

    /// <summary>本卡当前产品（未选为 null）。</summary>
    public FactoryProductData Product => curProduct;
    /// <summary>是否已选产品。</summary>
    public bool HasProduct => curProduct != null;
    /// <summary>本卡当前已选素材（只读）。</summary>
    public IReadOnlyList<ItemInfo> Materials => curMaterials;
    /// <summary>本卡花费（单价 × 数量），未选产品为 0。</summary>
    public int TotalCost => curProduct != null ? curProduct.TotalCost : 0;

    /// <summary>由主面板在 Instantiate 后调用：注入手办产品列表与变更回调，并复位为空卡。</summary>
    public void Set(IReadOnlyList<FactoryProductData> products, IReadOnlyList<ItemType> materialTypes, Action onChanged)
    {
        this.products = products;
        materialItemTypes = materialTypes;
        this.onChanged = onChanged;

        if(!bound)
        {
            addMaterialButton.onClick.AddListener(OpenMaterialSelect);
            foreach(Button slot in materialSlots)
                slot.onClick.AddListener(OpenMaterialSelect);
            productSlotButton.onClick.AddListener(OpenProductSelect);
            productItemButton.onClick.AddListener(OpenProductSelect);
            bound = true;
        }

        curMaterials.Clear();
        curProduct = null;
        RefreshMaterialSlots();
        RefreshProduct();
    }

    #region 素材槽
    // 已显示槽位数 = 已选素材数（至少 1 个空槽）；未达上限时显示「+」，已满（=槽位数）隐藏「+」
    void RefreshMaterialSlots()
    {
        int max = materialSlots.Count;
        visibleMaterialSlots = Mathf.Clamp(curMaterials.Count, 1, Mathf.Max(1, max));
        for(int i = 0; i < materialSlots.Count; i++)
            materialSlots[i].gameObject.SetActive(i < visibleMaterialSlots);
        addMaterialButton.gameObject.SetActive(curMaterials.Count < max);
        LayoutMaterialSlots();
        RefreshMaterialIcons();
    }

    // 把已显示槽位 +（可见时）添加按钮作为一组在容器内水平居中（HorLayout 为左对齐，这里改为居中）
    void LayoutMaterialSlots()
    {
        if(materialSlots.Count == 0)
            return;

        float slotW = ((RectTransform)materialSlots[0].transform).sizeDelta.x;
        float step = slotW + materialSlotSpacing;
        bool addVisible = addMaterialButton.gameObject.activeSelf;
        int units = visibleMaterialSlots + (addVisible ? 1 : 0);
        float startX = -(units - 1) * step * 0.5f;
        for(int i = 0; i < visibleMaterialSlots; i++)
            ((RectTransform)materialSlots[i].transform).anchoredPosition = new Vector2(startX + i * step, 0f);
    }

    // 已选素材依次填入槽位图标，空槽隐藏图标
    void RefreshMaterialIcons()
    {
        for(int i = 0; i < materialSlotIcons.Count; i++)
        {
            bool has = i < curMaterials.Count;
            materialSlotIcons[i].enabled = has;
            if(has)
                materialSlotIcons[i].SetIcon(curMaterials[i].IconPath);
        }
    }
    #endregion

    #region 产品
    void RefreshProduct()
    {
        bool hasProduct = curProduct != null;

        productSlotButton.gameObject.SetActive(!hasProduct);
        productGo.SetActive(hasProduct);
        productIcon.enabled = hasProduct;
        if(hasProduct)
            productIcon.SetIcon(curProduct.IconPath);

        productPriceText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.UnitPriceFmt,
            (LocalizeVarSet.FactoryMain.Price, hasProduct ? curProduct.UnitPrice : 0));
        productCountText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Main.CraftCountFmt,
            (LocalizeVarSet.FactoryMain.Count, hasProduct ? curProduct.CraftCount : 0));
    }
    #endregion

    #region 选择子面板
    void OpenMaterialSelect() =>
        UISystem.Instance.OpenUI<FactoryMaterialSelectPanel>(UIPanelIdSet.FactoryMaterialSelectPanel)
            .Show(materialItemTypes, curMaterials, OnMaterialsConfirmed);

    void OpenProductSelect() =>
        UISystem.Instance.OpenUI<FactoryProductSelectPanel>(UIPanelIdSet.FactoryProductSelectPanel)
            .Show(products, curProduct, OnProductConfirmed);

    void OnMaterialsConfirmed(List<ItemInfo> materials)
    {
        curMaterials.Clear();
        curMaterials.AddRange(materials);
        RefreshMaterialSlots();
        onChanged?.Invoke();
    }

    void OnProductConfirmed(FactoryProductData product)
    {
        curProduct = product;
        RefreshProduct();
        onChanged?.Invoke();
    }
    #endregion
}
