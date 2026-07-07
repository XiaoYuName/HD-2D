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
/// 每张卡独立完成 选择素材 → 选择产品；主面板汇总各卡花费为总金额，并负责 加工厂 / 升级设备 Tab、左侧工厂状态栏（等级 / 当前产量 / 产出良品率）、开始加工。
/// 任务卡由隐藏模板 <c>cardTemplate</c> 在运行时 Instantiate 到 <c>cardListContent</c>（横向 ScrollRect 的 Content）；素材数据复用物品系统（<see cref="PlayerBag"/>），产品种类取 <see cref="ItemConfig"/> 中的手办物品（<see cref="ItemType.Merchandise"/>）。
/// 当前产量 / 产出良品率 = <see cref="FactoryGameConfig"/> 基础值 + 设备升级加成之和（<see cref="FactoryEquipManager.SumBonus"/>，与小游戏口径一致）。
/// 备注：工厂等级、成本扣除等依赖策划数值，当前为占位（见待确认问题文档）；合作值已按设计图移除。
/// </summary>
public class FactoryMainPanel : UIBase
{
    [Title("配置")]
    [LabelText("工厂等级(占位)")][SerializeField] int factoryLevel = 1;
    [LabelText("小游戏配置(当前产量/良品率数值来源)")][SerializeField] FactoryGameConfig gameConfig;

    [Title("Tab")]
    [SerializeField] Button processTabButton;
    [LabelText("升级设备Tab按钮")][SerializeField] Button upgradeTabButton;
    [LabelText("模具管理Tab按钮")][SerializeField] Button moldMgButton;
    [SerializeField] GameObject processContent;
    [LabelText("升级设备内容")][SerializeField] GameObject upgradeContent;
    [LabelText("升级设备内容控制器")][SerializeField] FactoryUpgradePanel upgradePanel;

    [Title("工厂状态(左侧栏)")]
    [LabelText("等级文本")][SerializeField] LocalizeStringEvent levelText;
    [LabelText("当前产量数值文本")][SerializeField] LocalizeStringEvent volumeText;
    [LabelText("产出良品率数值文本")][SerializeField] LocalizeStringEvent yieldText;

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
        if(process)
            RefreshFactoryState();    // 从升级设备切回时设备加成可能已变化，刷新产量/良品率
        else if(upgradePanel != null)
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

        // 当前产量 / 产出良品率 = 小游戏基础值 + 设备升级加成（与 FactoryProcessGameManager 开局口径一致）
        FactoryEquipManager equip = FactoryEquipManager.St;
        int volume = (gameConfig != null ? gameConfig.BaseProductionVolume : 0)
            + (equip != null ? equip.SumBonus(FactoryEquipBonusType.ProductionVolume) : 0);
        int yield = Mathf.Clamp((gameConfig != null ? gameConfig.BaseYieldRate : 0)
            + (equip != null ? equip.SumBonus(FactoryEquipBonusType.Yield) : 0), 0, 100);
        volumeText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Main.VolumeFmt,
            (LocVarSet.FactoryMain.Volume, volume));
        yieldText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Main.YieldFmt,
            (LocVarSet.FactoryMain.Yield, yield));
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

    // 左侧状态栏配色（近设计图）
    static readonly Color LeftBarBg = new (0.99f, 0.98f, 0.96f, 1f);
    static readonly Color LeftChipBg = new (0.84f, 0.79f, 0.72f, 1f);
    static readonly Color LeftTextDark = new (0.30f, 0.27f, 0.24f, 1f);
    static readonly Color LeftPreviewGray = new (0.78f, 0.78f, 0.78f, 1f);

    [PropertySpace(8)]
    [Button("生成左侧工厂状态栏(仅改ProcessPanel下)", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.7f)]
    [InfoBox("只在「加工厂」内容(processContent)下生成左侧状态栏（单根节点 LeftStatusColumn，可整体挪位置/调尺寸）：\n" +
             "流水线预览图(占位) + 模具管理按钮 + 工厂等级条 + 当前产量/产出良品率条 + 底部等级提示，并改绑 levelText / volumeText / yieldText / moldMgButton。\n" +
             "同时清理 processContent 下旧的「合作值」文本（按 FactoryCoopFmt Key 识别）；旧等级文本/模具管理按钮在 processContent 下则删除重建，在外面则仅日志提醒手动删。重复点击会先清除上次生成的左侧栏。", InfoMessageType.Info)]
    void BuildLeftStatusColumn()
    {
        if(processContent == null)
        {
            Debug.LogWarning("[FactoryMainPanel] processContent 未赋值，无法生成左侧状态栏。", this);
            return;
        }
        Transform root = processContent.transform;

        // 清除上次生成
        Transform oldColumn = root.Find("LeftStatusColumn");
        if(oldColumn != null)
            DestroyImmediate(oldColumn.gameObject);

        // 清理旧「合作值」文本（按本地化 Key 识别，只动 processContent 下的；进度填充图若为其子物体会一并删除）
        foreach(LocalizeStringEvent lse in processContent.GetComponentsInChildren<LocalizeStringEvent>(true))
            if(ResolveEntryKeyName(lse) == FactoryLocKeySet.Main.CoopFmt)
            {
                Debug.Log($"[FactoryMainPanel] 已删除旧合作值文本：{GetPath(lse.transform)}（若进度填充图是独立物体请手动删除）", this);
                DestroyImmediate(lse.gameObject);
                break;
            }

        // 旧等级文本 / 模具管理按钮：改用新生成的，旧物体按位置删除或提醒
        CleanOldRef(levelText != null ? levelText.gameObject : null, "旧等级文本");
        CleanOldRef(moldMgButton != null ? moldMgButton.gameObject : null, "旧模具管理按钮");

        // 左侧栏根：默认贴在 processContent 左侧外沿
        RectTransform column = FactoryUIGen.Node("LeftStatusColumn", root);
        column.anchorMin = column.anchorMax = new Vector2(0f, 0.5f);
        column.pivot = new Vector2(1f, 0.5f);
        column.sizeDelta = new Vector2(500f, 950f);
        column.anchoredPosition = new Vector2(-30f, 0f);

        // 流水线预览图（占位灰图，美术图就绪后替换 Sprite）
        Image preview = FactoryUIGen.Img("PipelinePreview", column, LeftPreviewGray);
        FactoryUIGen.Anchor(preview.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 460f, 470f, 0f, 0f);

        // 模具管理按钮（重建并改绑 moldMgButton）
        Button mold = FactoryUIGen.Btn("MoldManageButton", column, FactoryLocKeySet.Main.MoldManage, LeftBarBg, LeftTextDark);
        FactoryUIGen.Anchor((RectTransform)mold.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 220f, 64f, 0f, -496f);

        // 工厂等级条：图标占位 + 等级文本
        Image levelBar = FactoryUIGen.Img("LevelBar", column, LeftBarBg);
        FactoryUIGen.Anchor(levelBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 500f, 70f, 0f, -588f);
        Image levelIcon = FactoryUIGen.Img("Icon", levelBar.transform, LeftPreviewGray);
        FactoryUIGen.Anchor(levelIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 46f, 46f, 12f, 0f);
        LocalizeStringEvent level = FactoryUIGen.Loc("LevelText", levelBar.transform, FactoryLocKeySet.Main.LevelFmt, 28, LeftTextDark, TextAlignmentOptions.Left);
        FactoryUIGen.Anchor(level.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 400f, 40f, 72f, 0f);

        // 当前产量 / 产出良品率条：两组「标签片 + 数值」
        Image statsBar = FactoryUIGen.Img("StatsBar", column, LeftBarBg);
        FactoryUIGen.Anchor(statsBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 500f, 56f, 0f, -670f);

        Image volChip = FactoryUIGen.Img("VolumeChip", statsBar.transform, LeftChipBg);
        FactoryUIGen.Anchor(volChip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 118f, 40f, 8f, 0f);
        LocalizeStringEvent volLabel = FactoryUIGen.Loc("Label", volChip.transform, FactoryLocKeySet.Main.VolumeLabel, 22, LeftTextDark, TextAlignmentOptions.Center);
        FactoryUIGen.Stretch(volLabel.GetComponent<RectTransform>());
        LocalizeStringEvent volume = FactoryUIGen.Loc("VolumeValue", statsBar.transform, FactoryLocKeySet.Main.VolumeFmt, 24, LeftTextDark, TextAlignmentOptions.Left);
        FactoryUIGen.Anchor(volume.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 108f, 36f, 134f, 0f);

        Image yieldChip = FactoryUIGen.Img("YieldChip", statsBar.transform, LeftChipBg);
        FactoryUIGen.Anchor(yieldChip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 132f, 40f, 252f, 0f);
        LocalizeStringEvent yieldLabel = FactoryUIGen.Loc("Label", yieldChip.transform, FactoryLocKeySet.Main.YieldLabel, 22, LeftTextDark, TextAlignmentOptions.Center);
        FactoryUIGen.Stretch(yieldLabel.GetComponent<RectTransform>());
        LocalizeStringEvent yield = FactoryUIGen.Loc("YieldValue", statsBar.transform, FactoryLocKeySet.Main.YieldFmt, 24, LeftTextDark, TextAlignmentOptions.Left);
        FactoryUIGen.Anchor(yield.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 96f, 36f, 394f, 0f);

        // 底部提示：等级越高解锁更多周边制作！
        LocalizeStringEvent hint = FactoryUIGen.Loc("LevelHint", column, FactoryLocKeySet.Main.LevelHint, 20, LeftTextDark, TextAlignmentOptions.Left);
        FactoryUIGen.Anchor(hint.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), 460f, 32f, 10f, -744f);

        levelText = level;
        volumeText = volume;
        yieldText = yield;
        moldMgButton = mold;
        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMainPanel] 左侧工厂状态栏已生成（processContent/LeftStatusColumn），引用已改绑。预览图为占位灰图，请替换美术资源。", this);
    }

    // 解析 LocalizeStringEvent 当前指向的条目 Key 名（兼容按 Id 引用的旧物体）
    static string ResolveEntryKeyName(LocalizeStringEvent lse)
    {
        var entry = lse.StringReference.TableEntryReference;
        if(entry.ReferenceType == UnityEngine.Localization.Tables.TableEntryReference.Type.Name)
            return entry.Key;
        var collection = UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection(lse.StringReference.TableReference);
        return collection != null ? entry.ResolveKeyName(collection.SharedData) : null;
    }

    // 旧引用物体在 processContent 下则删除（改用新生成的），在外面则日志提醒手动删（不越界改动）
    void CleanOldRef(GameObject go, string label)
    {
        if(go == null)
            return;
        if(go.transform.IsChildOf(processContent.transform))
        {
            Debug.Log($"[FactoryMainPanel] 已删除{label}：{GetPath(go.transform)}（改用新生成的）", this);
            DestroyImmediate(go);
        }
        else
            Debug.LogWarning($"[FactoryMainPanel] {label}不在 processContent 下，引用已改绑到新物体，旧物体请手动删除：{GetPath(go.transform)}", this);
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        for(Transform p = t.parent; p != null; p = p.parent)
            path = p.name + "/" + path;
        return path;
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
