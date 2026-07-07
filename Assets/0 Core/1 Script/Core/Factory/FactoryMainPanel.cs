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
/// 「加工厂」主界面：管理一排可水平滑动的制作任务卡（<see cref="FactoryTaskCard"/>），列表最右侧常驻「添加任务卡」按钮。
/// 每张卡独立完成 选择素材 → 选择产品；主面板汇总各卡花费为总金额，并负责 加工厂 / 升级设备 Tab、工厂等级 / 合作值、开始加工。
/// 任务卡由隐藏模板 <c>cardTemplate</c> 在运行时 Instantiate 到 <c>cardListContent</c>（横向 ScrollRect 的 Content）；素材数据复用物品系统（<see cref="PlayerBag"/>），产品种类取 <see cref="ItemConfig"/> 中的手办物品（<see cref="ItemType.Merchandise"/>）。
/// 备注：工厂等级 / 合作值、成本扣除、回收站等依赖策划数值，当前为占位（见待确认问题文档）。
/// </summary>
public class FactoryMainPanel : UIBase
{
    [Title("配置")]
    [LabelText("工厂等级(占位)")][SerializeField] int factoryLevel = 1;
    [LabelText("合作值当前(占位)")][SerializeField] int coopCur = 3;
    [LabelText("合作值上限(占位)")][SerializeField] int coopMax = 50;

    [Title("Tab")]
    [SerializeField] Button processTabButton;
    [LabelText("升级设备Tab按钮")][SerializeField] Button upgradeTabButton;
    [LabelText("模具管理Tab按钮")][SerializeField] Button moldMgButton;
    [SerializeField] GameObject processContent;
    [LabelText("升级设备内容")][SerializeField] GameObject upgradeContent;
    [LabelText("升级设备内容控制器")][SerializeField] FactoryUpgradePanel upgradePanel;

    [Title("工厂状态")]
    [LabelText("等级文本")][SerializeField] LocalizeStringEvent levelText;
    [LabelText("合作值文本")][SerializeField] LocalizeStringEvent coopText;
    [LabelText("合作值进度填充")][SerializeField] Image coopFill;

    [Title("制作任务卡 - 横向列表")]
    [LabelText("任务卡模板(隐藏，运行时克隆)")][SerializeField] FactoryTaskCard cardTemplate;
    [LabelText("任务卡容器(横向ScrollRect的Content)")][SerializeField] RectTransform cardListContent;
    [LabelText("添加任务卡按钮(常驻列表最右)")][SerializeField] Button addCardButton;
    [LabelText("任务卡数量上限(0=不限)"), MinValue(0)][SerializeField] int maxCards = 0;

    [Title("结算 / 其它")]
    [LabelText("总金额数值文本(纯数字，颜色/字号在UI上调)")][SerializeField] TMP_Text totalCostValueText;
    [LabelText("开始加工")][SerializeField] Button startButton;
    [LabelText("关闭")][SerializeField] Button closeButton;
    [LabelText("未选产品提示")][SerializeField] WarnTip warnTip;

    readonly List<FactoryTaskCard> cards = new ();

    #region 生命周期
    public override void Init()
    {

    }
    void Awake()
    {
        processTabButton.onClick.AddListener(() => SwitchTab(true));
        upgradeTabButton.onClick.AddListener(() => SwitchTab(false));
        moldMgButton.onClick.AddListener(OnMoldMgButton);
        addCardButton.onClick.AddListener(OnAddCardButton);
        startButton.onClick.AddListener(OnStartButton);
        closeButton.onClick.AddListener(OnCloseButton);
        cardTemplate.gameObject.SetActive(false);
    }
    public override void Open()
    {
        base.Open();
        SwitchTab(true);
        Refresh();
    }

    // 重建工厂状态 / 产品列表 / 任务卡。开局与「从小游戏结算返回」时调用：
    // 返回时背包可能已因加工消耗了素材，重建任务卡可把失效（已无）的素材选择清空。
    void Refresh()
    {
        RefreshFactoryState();
        RebuildCards();
    }
    #endregion
    void OnMoldMgButton()
    {
        UISystem.Instance.OpenUI(UIPanelIdSet.FactoryMoldMgPanel);
    }

    #region Tab
    void SwitchTab(bool process)
    {
        processContent.SetActive(process);
        upgradeContent.SetActive(!process);
        if(!process && upgradePanel != null)
            upgradePanel.Refresh();   // 切到升级设备时重建设备列表
    }
    #endregion

    #region 任务卡
    // 清空已有任务卡并以一张空卡起步（添加按钮保持在最右）
    void RebuildCards()
    {
        for(int i = cards.Count - 1; i >= 0; i--)
            Destroy(cards[i].gameObject);
        cards.Clear();
        AddCard();
    }

    void OnAddCardButton() => AddCard();

    // 克隆模板生成一张空卡，加入列表；「添加」按钮始终保持在最右，达上限时隐藏
    FactoryTaskCard AddCard()
    {
        if(maxCards > 0 && cards.Count >= maxCards)
            return null;

        FactoryTaskCard card = Instantiate(cardTemplate, cardListContent);
        card.gameObject.SetActive(true);
        card.Set(ItemType.FactoryProductionMaterials, RefreshTotal);
        cards.Add(card);

        addCardButton.transform.SetAsLastSibling();
        addCardButton.gameObject.SetActive(maxCards <= 0 || cards.Count < maxCards);
        RefreshTotal();
        return card;
    }
    #endregion

    #region 刷新
    void RefreshFactoryState()
    {
        levelText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Main.LevelFmt,
            (LocVarSet.FactoryMain.Level, factoryLevel));
        coopText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Main.CoopFmt,
            (LocVarSet.FactoryMain.CoopCur, coopCur), (LocVarSet.FactoryMain.CoopMax, coopMax));
        coopFill.fillAmount = coopMax > 0 ? coopCur / (float)coopMax : 0f;
    }

    // 任务卡「选产品」流程已断开（见 FactoryTaskCard 注释），已无花费来源可汇总，恒显示 0
    void RefreshTotal()
    {
        totalCostValueText.text = "0";
    }
    #endregion

    #region 按钮
    // 开始加工：弹出产品选择面板，列出背包中「生产资料(模具)」(FactoryProductionMaterials)，选一个确认后进入下压小游戏。
    // 原「多任务卡各选素材+产品、汇总为批次」的流程暂不使用，见下方 #if false（任务卡列表本身仍保留展示，仅开始加工不再依赖它）。
    void OnStartButton()
    {
        List<FactoryMoldItemInfo> materials = BuildMaterialProducts();
        if(materials.Count == 0)
        {
            warnTip.ShowTip(LocTableSet.Factory, FactoryLocKeySet.Main.NeedProduct);
            return;
        }

        UISystem.Instance.OpenUI<FactoryProductSelectPanel>(UIPanelIdSet.FactoryProductSelectPanel)
            .Show(materials, null, OnMaterialConfirmed);
    }

    // 背包中收集全部「生产资料(模具)」物品：均为运行时自描述物品(FactoryMoldItemInfo)，图标/名称/单价随实例携带，不查 ItemConfig。
    List<FactoryMoldItemInfo> BuildMaterialProducts()
    {
        List<FactoryMoldItemInfo> result = new ();
        InventoryManager bag = InventoryManager.Instance;
        if(bag == null)
            return result;

        foreach(ItemInfo m in bag.GetItemList(ItemType.FactoryProductionMaterials))
            if(m is FactoryMoldItemInfo material)
                result.Add(material);
        return result;
    }

    // 选定生产资料后：以其为本局唯一加工批次打开小游戏；结束后按完成率发放对应「周边商品(Merchandise)」（见 FactoryProcessPanel.GrantProducts）。
    void OnMaterialConfirmed(FactoryMoldItemInfo material)
    {
        FactoryProcessPanel panel = UISystem.Instance.OpenUI<FactoryProcessPanel>(UIPanelIdSet.FactoryProcessPanel);
        panel.SetCraftBatch(new List<FactoryMoldItemInfo> { material });
        panel.SetOnClosed(Refresh);   // 小游戏（含结算）关闭返回本面板时刷新
    }

#if false
    // 原「多任务卡批量加工」流程：至少一张卡已选产品才进入下压小游戏，把各卡所选产品汇总为批次带入小游戏，
    // 并消耗各卡所选素材。成本（金额）扣除依赖策划数值，暂未接入（见待确认问题文档）。暂不使用，保留以备后续恢复。
    void OnStartButton_TaskCardBatch()
    {
        if(!cards.Exists(c => c.HasProduct))
        {
            warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Main.NeedProduct);
            return;
        }

        List<FactoryProductData> batch = new ();
        foreach(FactoryTaskCard card in cards)
            if(card.HasProduct)
            {
                batch.Add(card.Product);
                ConsumeMaterials(card);
            }

        FactoryProcessPanel panel = UISystem.Instance.OpenUI<FactoryProcessPanel>(UIPanelIdSet.FactoryProcessPanel);
        panel.SetCraftBatch(batch);
        panel.SetOnClosed(Refresh);   // 小游戏（含结算）关闭返回本面板时刷新，清掉已被消耗的素材选择
    }

    // 消耗本卡所选素材：每个所选素材各扣 1 个（占位数量，待策划配方数量确定后再改）。
    // card.Materials 即背包中的物品实例引用，扣到 0 由 PlayerBag 自动移除。
    void ConsumeMaterials(FactoryTaskCard card)
    {
        InventoryManager bag = InventoryManager.Instance;
        if(bag == null)
            return;

        foreach(ItemInfo m in card.Materials)
            if(m != null && m.Count > 0)
                bag.ConsumeItem(m, 1);
    }
#endif

    void OnCloseButton() => UISystem.Instance.CloseUI(uiname);
    #endregion

#if UNITY_EDITOR
    #region 一键生成（仅编辑器）
    [PropertySpace(8)]
    [Button("生成任务卡横向容器", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("在「加工厂」内容(processContent)下生成一个横向可左右滑动的任务卡列表(ScrollRect)，并在其中放入常驻最右的「添加任务卡(+)」按钮，自动赋值 cardListContent / addCardButton。\n" +
             "任务卡模板(cardTemplate)请把你的 TaskCard 手动拖入对应字段；生成的容器默认充满内容区，可整体调位置 / 尺寸。重复点击会先清除上次生成的容器。", InfoMessageType.Info)]
    void BuildTaskCardList()
    {
        // 清除上次生成的容器，避免重复叠加
        if(cardListContent != null)
        {
            ScrollRect old = cardListContent.GetComponentInParent<ScrollRect>();
            if(old != null)
                DestroyImmediate(old.gameObject);
            cardListContent = null;
            addCardButton = null;
        }

        Transform parent = processContent != null ? processContent.transform : transform;
        RectTransform content = FactoryUIGen.HorizontalScrollList("TaskCardScrollView", parent);
        cardListContent = content;

        // 「添加」占位按钮尺寸：取模板卡尺寸，未指定时用与现有 TaskCard 一致的 380×520
        Vector2 cardSize = cardTemplate != null ? ((RectTransform)cardTemplate.transform).sizeDelta : new Vector2(380, 520);

        Image addImg = FactoryUIGen.Img("AddCardButton", content, new Color(0f, 0f, 0f, 0.04f));
        FactoryUIGen.Center(addImg.rectTransform, cardSize.x, cardSize.y, 0, 0);
        addCardButton = addImg.gameObject.AddComponent<Button>();
        addCardButton.targetGraphic = addImg;
        TMP_Text plus = FactoryUIGen.Text("Plus", addImg.transform, "+", 90, new Color(0.55f, 0.55f, 0.6f), TextAlignmentOptions.Center);
        FactoryUIGen.Stretch(plus.rectTransform);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMainPanel] 任务卡横向容器已生成。把 TaskCard 拖到 cardTemplate 字段即可运行。", this);
    }
    #endregion

    #region 测试（仅编辑器）
    const int TestMaterialCombos = 4;   // 取几组「框架×贴纸」组合，覆盖不同外观与价位
    const int TestMaterialCount = 5;

    [PropertySpace(8)]
    [Button("测试：添加示例生产资料(模具)到背包", ButtonSizes.Large), GUIColor(1f, 0.8f, 0.5f)]
    [InfoBox("运行时点击：现取几组「框架(FigureModel)×贴纸(Painting)」组合，现场合成运行时自描述生产资料(FactoryMoldItemInfo)加入背包，" +
             "免去先在物料制作面板逐个合成，方便直接测试「开始加工」选择/加工/发放商品的完整流程。", InfoMessageType.Info)]
    void TestAddSampleMaterials()
    {
        InventoryManager bag = InventoryManager.Instance;
        if(bag == null)
        {
            Debug.LogWarning("[FactoryMainPanel] 未找到 PlayerInfo/背包，需在运行时(Play 模式)点击此按钮。", this);
            return;
        }

        List<ItemData> frames = new (), paintings = new ();
        foreach(ItemData item in ItemManager.St.Config.ItemDataDict.Values)
        {
            if(item == null)
                continue;
            if(item.Type == ItemType.FigureModel)
                frames.Add(item);
            else if(item.Type == ItemType.Painting)
                paintings.Add(item);
        }
        if(frames.Count == 0 || paintings.Count == 0)
        {
            Debug.LogWarning("[FactoryMainPanel] ItemConfig 里没有 FigureModel/Painting 物品，无法生成示例生产资料。", this);
            return;
        }
        frames.Sort((a, b) => a.Id.CompareTo(b.Id));
        paintings.Sort((a, b) => a.Id.CompareTo(b.Id));

        int n = Mathf.Min(TestMaterialCombos, Mathf.Min(frames.Count, paintings.Count));
        for(int i = 0; i < n; i++)
        {
            ItemInfo frameInfo = ItemInfo.Create(frames[i].Id, 1);
            ItemInfo paintingInfo = ItemInfo.Create(paintings[i].Id, 1);
            FactoryMoldItemInfo material = FactoryMoldItemInfo.Create(frameInfo, paintingInfo, TestMaterialCount, frames[i].Value + paintings[i].Value);
            bag.AddRuntimeItem(material);
        }

        Debug.Log($"[FactoryMainPanel] 已添加 {n} 组测试生产资料，每组 x{TestMaterialCount}。", this);
    }
    #endregion
#endif
}
