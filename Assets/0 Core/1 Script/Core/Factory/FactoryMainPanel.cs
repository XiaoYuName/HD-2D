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
/// 「加工厂」主界面：在网格容器中直接铺出背包内全部生产资料(模具，<see cref="FactoryMoldItemInfo"/>)的格子（<see cref="FactoryComposedItemCellUI"/>），
/// 单选高亮后点击「加工」直接携带所选资料进入下压小游戏（<see cref="FactoryProcessPanel"/>），无需再弹出独立选择面板。
/// 另负责 加工厂 / 升级设备 Tab、左侧工厂状态栏（当前产量 / 产出良品率）。
/// 格子由隐藏模板 <c>cellTemplate</c> 在运行时 Instantiate 到 <c>gridContent</c>（GridLayoutGroup 所在的 Content）。
/// 当前产量 / 产出良品率 = <see cref="FactoryGameConfig"/> 基础值 + 设备升级加成之和（<see cref="FactoryEquipManager.SumBonus"/>，与小游戏口径一致）。
/// 备注：工厂等级、成本扣除等依赖策划数值，当前为占位（见待确认问题文档）；合作值已按设计图移除。
/// </summary>
public class FactoryMainPanel : UIBase
{
    [Title("配置")]
    [LabelText("工厂等级(占位)")][SerializeField] int factoryLevel = 0;
    [LabelText("小游戏配置(当前产量/良品率数值来源)")][SerializeField] FactoryGameConfig gameConfig;

    [Title("Tab")]
    [SerializeField] Button processTabButton;
    [LabelText("升级设备Tab按钮")][SerializeField] Button upgradeTabButton;
    [LabelText("模具管理Tab按钮")][SerializeField] Button moldMgButton;
    [SerializeField] GameObject processContent;
    [LabelText("升级设备内容")][SerializeField] GameObject upgradeContent;
    [LabelText("升级设备内容控制器")][SerializeField] FactoryUpgradePanel upgradePanel;

    [Title("工厂状态(左侧栏)")]
    [LabelText("当前产量数值文本")][SerializeField] LocalizeStringEvent volumeText;
    [LabelText("产出良品率数值文本")][SerializeField] LocalizeStringEvent yieldText;

    [Title("生产资料格子 - 网格列表")]
    [LabelText("格子模板(隐藏，运行时克隆)")][SerializeField] FactoryComposedItemCellUI cellTemplate;
    [LabelText("格子容器(GridLayoutGroup所在的Content)")][SerializeField] RectTransform gridContent;
    [LabelText("暂无生产资料提示(可选)")][SerializeField] GameObject emptyHint;

    [Title("结算 / 其它")]
    [LabelText("总金额数值文本(纯数字，颜色/字号在UI上调)")][SerializeField] TMP_Text totalCostValueText;
    [LabelText("开始加工")][SerializeField] Button startButton;
    [LabelText("关闭")][SerializeField] Button closeButton;
    [LabelText("未选产品提示")][SerializeField] WarnTip warnTip;

    readonly List<FactoryComposedItemCellUI> cells = new ();
    readonly List<FactoryMoldItemInfo> materials = new ();
    int selectedIndex = -1;

    #region 生命周期
    public override void Init()
    {

    }
    void Awake()
    {
        processTabButton.onClick.AddListener(() => SwitchTab(true));
        upgradeTabButton.onClick.AddListener(() => SwitchTab(false));
        moldMgButton.onClick.AddListener(OnMoldMgButton);
        startButton.onClick.AddListener(OnStartButton);
        closeButton.onClick.AddListener(OnCloseButton);
        cellTemplate.gameObject.SetActive(false);
    }

    // 设备升级会实时影响左侧产量/良品率，无论当前停留在哪个 Tab 都需要同步刷新
    void OnEnable()
    {
        FactoryEquipManager.St.OnEquipChanged += OnEquipChanged;
    }
    void OnDisable()
    {
        FactoryEquipManager.St.OnEquipChanged -= OnEquipChanged;
    }
    void OnEquipChanged(int id) => RefreshFactoryState();

    public override void Open()
    {
        base.Open();
        SwitchTab(true);
        Refresh();
    }

    // 重建工厂状态 / 生产资料格子。开局与「从小游戏结算返回」时调用：
    // 返回时背包可能已因加工消耗了素材，重建格子可把已消耗（已无）的选中项清空。
    void Refresh()
    {
        RefreshFactoryState();
        RebuildGrid();
    }
    #endregion
    void OnMoldMgButton()
    {
        FactoryMoldMgPanel panel = UISystem.Instance.OpenUI<FactoryMoldMgPanel>(UIPanelIdSet.FactoryMoldMgPanel);
        panel.SetOnClosed(Refresh);   // 物料制作面板关闭返回时刷新，让新合成的模具出现在生产资料列表
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

    #region 生产资料格子
    // 按背包现有生产资料(模具)重建网格格子；默认单选第一个（如有），并同步暂无提示的显隐
    void RebuildGrid()
    {
        for(int i = gridContent.childCount - 1; i >= 0; i--)
        {
            Transform child = gridContent.GetChild(i);
            if(child == cellTemplate.transform)
                continue;
            Destroy(child.gameObject);
        }
        cells.Clear();
        materials.Clear();
        materials.AddRange(BuildMaterialProducts());
        selectedIndex = -1;

        for(int i = 0; i < materials.Count; i++)
        {
            FactoryComposedItemCellUI cell = Instantiate(cellTemplate, gridContent);
            cell.gameObject.SetActive(true);
            cell.Set(materials[i]);   // 图标(三层合成)/名称/数量/单价均由物品自身携带
            cell.Set(i, OnCellClick);
            cells.Add(cell);
        }
        if(materials.Count > 0)
            OnCellClick(0);

        if(emptyHint != null)
            emptyHint.SetActive(materials.Count == 0);
        RefreshTotal();
    }

    // 单选：点击格子切换高亮
    void OnCellClick(int index)
    {
        if(index < 0 || index >= cells.Count)
            return;
        if(selectedIndex >= 0 && selectedIndex < cells.Count)
            cells[selectedIndex].SetSelected(false);
        selectedIndex = index;
        cells[index].SetSelected(true);
    }
    #endregion

    #region 刷新
    void RefreshFactoryState()
    {
        // 当前产量 / 产出良品率 = 小游戏基础值 + 设备升级加成（与 FactoryProcessGameManager 开局口径一致）
        FactoryEquipManager equip = FactoryEquipManager.St;
        int volume = (gameConfig != null ? gameConfig.BaseProductionVolume : 0)
            + equip.SumBonus(FactoryEquipBonusType.ProductionVolume);
        int yield = Mathf.Clamp((gameConfig != null ? gameConfig.BaseYieldRate : 0)
            + equip.SumBonus(FactoryEquipBonusType.Yield), 0, 100);
        volumeText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Main.VolumeFmt,
            (LocVarSet.FactoryMain.Volume, volume));
        yieldText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Main.YieldFmt,
            (LocVarSet.FactoryMain.Yield, yield));
    }

    // 「选产品」流程已断开，已无花费来源可汇总，恒显示 0（成本扣除依赖策划数值，见待确认问题文档）
    void RefreshTotal()
    {
        totalCostValueText.text = "0";
    }
    #endregion

    #region 按钮
    // 开始加工：直接取网格中已选中的生产资料(模具)为本局唯一加工批次进入下压小游戏；
    // 原「弹出独立选择面板 FactoryProductSelectPanel 再确认」的流程已合并——选择即在本面板网格完成。
    void OnStartButton()
    {
        if(selectedIndex < 0 || selectedIndex >= materials.Count)
        {
            warnTip.ShowTip(LocTableSet.Factory, FactoryLocKeySet.Main.NeedProduct);
            return;
        }

        FactoryMoldItemInfo material = materials[selectedIndex];
        FactoryProcessPanel panel = UISystem.Instance.OpenUI<FactoryProcessPanel>(UIPanelIdSet.FactoryProcessPanel);
        panel.SetCraftBatch(new List<FactoryMoldItemInfo> { material });
        panel.SetOnClosed(Refresh);   // 小游戏（含结算）关闭返回本面板时刷新，清掉已被消耗的选中项
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

    void OnCloseButton() => UISystem.Instance.CloseUI(uiname);
    #endregion

#if UNITY_EDITOR
    #region 一键生成（仅编辑器）
    // 左侧状态栏配色（近设计图）
    static readonly Color LeftBarBg = new (0.99f, 0.98f, 0.96f, 1f);
    static readonly Color LeftChipBg = new (0.84f, 0.79f, 0.72f, 1f);
    static readonly Color LeftTextDark = new (0.30f, 0.27f, 0.24f, 1f);
    static readonly Color LeftPreviewGray = new (0.78f, 0.78f, 0.78f, 1f);

    [PropertySpace(8)]
    [Button("生成左侧工厂状态栏(仅改ProcessPanel下)", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.7f)]
    [InfoBox("只在「加工厂」内容(processContent)下生成左侧状态栏（单根节点 LeftStatusColumn，可整体挪位置/调尺寸）：\n" +
             "流水线预览图(占位) + 模具管理按钮 + 当前产量/产出良品率条，并改绑 volumeText / yieldText / moldMgButton。\n" +
             "同时清理 processContent 下旧的「合作值」文本（按 FactoryCoopFmt Key 识别）；旧模具管理按钮在 processContent 下则删除重建，在外面则仅日志提醒手动删。重复点击会先清除上次生成的左侧栏。", InfoMessageType.Info)]
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

        // 旧模具管理按钮：改用新生成的，旧物体按位置删除或提醒
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

        // 当前产量 / 产出良品率条：两组「标签片 + 数值」
        Image statsBar = FactoryUIGen.Img("StatsBar", column, LeftBarBg);
        FactoryUIGen.Anchor(statsBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 500f, 56f, 0f, -588f);

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
            Debug.LogWarning("[FactoryMainPanel] 未找到背包，需在运行时(Play 模式)点击此按钮。", this);
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
            FactoryMoldItemInfo material = FactoryMoldItemInfo.Create(frameInfo, paintingInfo, TestMaterialCount);
            bag.AddRuntimeItem(material);
        }

        Debug.Log($"[FactoryMainPanel] 已添加 {n} 组测试生产资料，每组 x{TestMaterialCount}。", this);
    }
    #endregion
#endif
}
