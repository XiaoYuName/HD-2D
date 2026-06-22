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
/// 每张卡独立完成 选择素材 → 选择产品；主面板汇总各卡花费为总金额，并负责 加工厂 / 回收站 Tab、工厂等级 / 合作值、开始加工。
/// 任务卡由隐藏模板 <c>cardTemplate</c> 在运行时 Instantiate 到 <c>cardListContent</c>（横向 ScrollRect 的 Content）；素材数据复用物品系统（<see cref="PlayerBag"/>），产品种类来自 <see cref="FactoryProductConfig"/>。
/// 备注：工厂等级 / 合作值、成本扣除、回收站等依赖策划数值，当前为占位（见待确认问题文档）。
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
        recycleTabButton.onClick.AddListener(() => SwitchTab(false));
        addCardButton.onClick.AddListener(OnAddCardButton);
        startButton.onClick.AddListener(OnStartButton);
        closeButton.onClick.AddListener(OnCloseButton);
        if(cardTemplate != null)
            cardTemplate.gameObject.SetActive(false);
    }
    public override void Open()
    {
        base.Open();
        SwitchTab(true);
        RefreshFactoryState();
        RebuildCards();
    }
    #endregion

    #region Tab
    void SwitchTab(bool process)
    {
        processContent.SetActive(process);
        recycleContent.SetActive(!process);
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
        card.Setup(productConfig, materialItemType, RefreshTotal);
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
        levelText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Main.LevelFmt,
            (LocalizeVarSet.FactoryMain.Level, factoryLevel));
        coopText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Main.CoopFmt,
            (LocalizeVarSet.FactoryMain.CoopCur, coopCur), (LocalizeVarSet.FactoryMain.CoopMax, coopMax));
        coopFill.fillAmount = coopMax > 0 ? coopCur / (float)coopMax : 0f;
    }

    // 汇总各任务卡花费为总金额
    void RefreshTotal()
    {
        int total = 0;
        foreach(FactoryTaskCard card in cards)
            total += card.TotalCost;
        totalCostValueText.text = total.ToString();
    }
    #endregion

    #region 按钮
    // 开始加工：至少一张卡已选产品才进入下压小游戏。成本扣除 / 素材消耗依赖策划数值，暂未接入（见待确认问题文档）。
    void OnStartButton()
    {
        if(!cards.Exists(c => c.HasProduct))
        {
            warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Main.NeedProduct);
            return;
        }
        UISystem.Instance.OpenUI(UIPanelIdSet.FactoryProcessPanel);
    }

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
#endif
}
