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
/// 「加工厂」主界面：选择素材 → 选择产品 → 开始加工（进入下压小游戏 <see cref="FactoryProcessPanel"/>）。
/// 含 加工厂 / 回收站 Tab、传送带预览、工厂等级 / 合作值、制作任务栏、总金额、NPC 立绘。
/// 素材数据复用物品系统（<see cref="PlayerBag"/>），产品种类来自 <see cref="FactoryProductConfig"/>。
/// 备注：工厂等级 / 合作值、成本扣除、回收站、左右滑动多任务等依赖策划数值，当前为占位（见待确认问题文档）。
/// </summary>
public class FactoryMainPanel : UIBase
{
    [Title("配置")]
    [LabelText("产品配置")][SerializeField] FactoryProductConfig productConfig;
    [LabelText("素材物品类型(None=全部)")][SerializeField] ItemType materialItemType = ItemType.None;
    [LabelText("工厂等级(占位)")][SerializeField] int factoryLevel = 1;
    [LabelText("合作值当前(占位)")][SerializeField] int coopCur = 3;
    [LabelText("合作值上限(占位)")][SerializeField] int coopMax = 50;

    [Title("Tab")]
    [SerializeField] Button processTabButton;
    [SerializeField] Button recycleTabButton;
    [SerializeField] GameObject processContent;
    [SerializeField] GameObject recycleContent;

    [Title("工厂状态")]
    [LabelText("等级文本")][SerializeField] LocalizeStringEvent levelText;
    [LabelText("合作值文本")][SerializeField] LocalizeStringEvent coopText;
    [LabelText("合作值进度填充")][SerializeField] Image coopFill;

    [Title("制作任务栏 - 素材")]
    [LabelText("添加素材按钮(+)")][SerializeField] Button addMaterialButton;
    [LabelText("素材槽位(首个常显，点+依次显示)")][SerializeField] List<Button> materialSlots = new ();
    [LabelText("素材槽图标(与槽位一一对应)")][SerializeField] List<Image> materialSlotIcons = new ();
    [LabelText("素材槽容器(居中排列)")][SerializeField] RectTransform materialSlotContainer;
    [LabelText("素材槽间距")][SerializeField] float materialSlotSpacing = 24f;

    [Title("制作任务栏 - 产品")]
    [LabelText("产品槽按钮")][SerializeField] Button productSlotButton;
    [LabelText("产品图标")][SerializeField] Image productIcon;
    [LabelText("产品单价文本")][SerializeField] LocalizeStringEvent productPriceText;
    [LabelText("产品数量文本")][SerializeField] LocalizeStringEvent productCountText;

    [Title("结算 / 其它")]
    [LabelText("总金额数值文本(纯数字，颜色/字号在UI上调)")][SerializeField] TMP_Text totalCostValueText;
    [LabelText("开始加工")][SerializeField] Button startButton;
    [LabelText("关闭")][SerializeField] Button closeButton;
    [LabelText("未选产品提示")][SerializeField] WarnTip warnTip;

    FactoryProductData curProduct;
    readonly List<ItemInfo> curMaterials = new ();
    int visibleMaterialSlots = 1;

    #region 生命周期
    public override void Init()
    {
        processTabButton.onClick.AddListener(() => SwitchTab(true));
        recycleTabButton.onClick.AddListener(() => SwitchTab(false));
        addMaterialButton.onClick.AddListener(OnAddMaterialSlot);
        foreach(Button slot in materialSlots)
            slot.onClick.AddListener(OpenMaterialSelect);
        productSlotButton.onClick.AddListener(OpenProductSelect);
        startButton.onClick.AddListener(OnStartButton);
        closeButton.onClick.AddListener(OnCloseButton);
    }

    public override void Open()
    {
        base.Open();
        SwitchTab(true);
        ResetMaterialSlots();
        RefreshFactoryState();
        RefreshTaskCard();
    }
    #endregion

    #region Tab
    void SwitchTab(bool process)
    {
        processContent.SetActive(process);
        recycleContent.SetActive(!process);
    }
    #endregion

    #region 素材槽
    // 复位为只显首个槽位、显示「+」按钮并居中
    void ResetMaterialSlots()
    {
        visibleMaterialSlots = 1;
        for(int i = 0; i < materialSlots.Count; i++)
            materialSlots[i].gameObject.SetActive(i < visibleMaterialSlots);
        addMaterialButton.gameObject.SetActive(materialSlots.Count > 1);
        LayoutMaterialSlots();
    }

    // 点「+」：依次显示下一个槽位，满槽后隐藏「+」，并重新居中
    void OnAddMaterialSlot()
    {
        if(visibleMaterialSlots >= materialSlots.Count)
            return;

        materialSlots[visibleMaterialSlots].gameObject.SetActive(true);
        visibleMaterialSlots++;
        if(visibleMaterialSlots >= materialSlots.Count)
            addMaterialButton.gameObject.SetActive(false);
        LayoutMaterialSlots();
    }

    // 将已显示的槽位在容器内水平居中排列（HorLayout 为左对齐，这里改为居中）
    void LayoutMaterialSlots()
    {
        if(materialSlots.Count == 0)
            return;

        float slotW = ((RectTransform)materialSlots[0].transform).sizeDelta.x;
        float step = slotW + materialSlotSpacing;
        float startX = -(visibleMaterialSlots - 1) * step * 0.5f;
        for(int i = 0; i < visibleMaterialSlots; i++)
            ((RectTransform)materialSlots[i].transform).anchoredPosition = new Vector2(startX + i * step, 0f);
    }
    #endregion

    #region 刷新
    void RefreshFactoryState()
    {
        levelText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Main.LevelFmt,
            (LocalizeVarSet.FactoryMain.Level, factoryLevel));
        coopText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Main.CoopFmt,
            (LocalizeVarSet.FactoryMain.CoopCur, coopCur), (LocalizeVarSet.FactoryMain.CoopMax, coopMax));
        coopFill.fillAmount = coopMax > 0 ? coopCur / (float)coopMax : 0f;
    }

    void RefreshTaskCard()
    {
        bool hasProduct = curProduct != null;
        productIcon.enabled = hasProduct;
        if(hasProduct)
            productIcon.SetIcon(curProduct.IconPath);

        productPriceText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.UnitPriceFmt,
            (LocalizeVarSet.FactoryMain.Price, hasProduct ? curProduct.UnitPrice : 0));
        productCountText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Main.CraftCountFmt,
            (LocalizeVarSet.FactoryMain.Count, hasProduct ? curProduct.CraftCount : 0));
        totalCostValueText.text = (hasProduct ? curProduct.TotalCost : 0).ToString();

        RefreshMaterialIcons();
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

    #region 选择子面板
    void OpenMaterialSelect() =>
        UISystem.Instance.OpenUI<FactoryMaterialSelectPanel>(UIPanelIdSet.FactoryMaterialSelectPanel)
            .Show(materialItemType, curMaterials, OnMaterialsConfirmed);

    void OpenProductSelect() =>
        UISystem.Instance.OpenUI<FactoryProductSelectPanel>(UIPanelIdSet.FactoryProductSelectPanel)
            .Show(productConfig, curProduct, OnProductConfirmed);

    void OnMaterialsConfirmed(List<ItemInfo> materials)
    {
        curMaterials.Clear();
        curMaterials.AddRange(materials);
        RefreshMaterialIcons();
    }

    void OnProductConfirmed(FactoryProductData product)
    {
        curProduct = product;
        RefreshTaskCard();
    }
    #endregion

    #region 按钮
    // 开始加工：校验已选产品后进入下压小游戏。成本扣除 / 素材消耗依赖策划数值，暂未接入（见待确认问题文档）。
    void OnStartButton()
    {
        if(curProduct == null)
        {
            warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Main.NeedProduct);
            return;
        }
        UISystem.Instance.OpenUI(UIPanelIdSet.FactoryProcessPanel);
    }

    void OnCloseButton() => UISystem.Instance.CloseUI(uiname);
    #endregion
}
