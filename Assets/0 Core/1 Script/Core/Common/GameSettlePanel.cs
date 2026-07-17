using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using TMPro;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
#endif

/// <summary>
/// 通用「本局结算」面板：可被各小游戏复用（女巫毒药、幸运转盘等）。
/// 通过 <see cref="Show"/> 传入头像、台词、中间内容、道具提示、再来一局消耗体力与按钮回调，
/// 文本全部走多语言表（默认 <see cref="LocTableSet.CasinoGame"/>）。
/// 用法：UISystem.Instance.OpenUI&lt;CasinoGameSettlePanel&gt;("CasinoGameSettlePanel").Show(data);
/// </summary>
public class GameSettlePanel : UIBase
{
    [Title("Ref")]
    [LabelText("左侧头像")][SerializeField] Image avatarImage;
    [LabelText("台词")][SerializeField] LocalizeStringEvent speechText;
    [LabelText("标题（本局结算）")][SerializeField] LocalizeStringEvent titleText;
    [LabelText("中间内容文本")][SerializeField] LocalizeStringEvent contentText;
    [LabelText("道具提示文本")][SerializeField] LocalizeStringEvent itemHintText;
    [LabelText("道具提示根节点（可隐藏）")][SerializeField] GameObject itemHintRoot;
    [LabelText("再来一局消耗体力文本")][SerializeField] LocalizeStringEvent playAgainCostText;
    [LabelText("再来一局条件提示")][SerializeField] WarnTip warnTip;
    [Title("Button")]
    [LabelText("再来一局")][SerializeField] Button playAgainButton;
    [LabelText("返回")][SerializeField] Button backButton;
    [SerializeReference] Data curData;

    /// <summary>结算面板展示所需的全部数据；文本字段均为多语言 Key。</summary>
    public class Data
    {
        /// <summary>左侧精灵图头像；为 null 时隐藏头像。</summary>
        public Sprite Avatar;
        /// <summary>多语言表名，为空时取 <see cref="LocTableSet.CasinoGame"/>。</summary>
        public string Table = LocTableSet.CasinoGame;
        /// <summary>标题 Key，为空时保留面板上现有标题。</summary>
        public string TitleKey;
        /// <summary>台词 Key。</summary>
        public string SpeechKey;
        /// <summary>中间内容 Key（可含 {占位符}）。</summary>
        public string ContentKey;
        /// <summary>中间内容的占位符（名称 + 值，值支持 int/float/bool/string）。</summary>
        public (string name, object value)[] ContentVars;
        /// <summary>获得物品提示 Key，为空时隐藏该提示。</summary>
        public string ItemHintKey;
        /// <summary>再来一局消耗体力；&lt;=0 时隐藏消耗文本。</summary>
        public int PlayAgainSpCost;
        /// <summary>再来一局按钮是否可点。</summary>
        public bool PlayAgainInteractable = true;
        /// <summary>再来一局条件判断：返回 false 则不触发 <see cref="OnPlayAgain"/>，并弹出 <see cref="PlayAgainFailTipKey"/> 提示。为空视为始终满足。</summary>
        public Func<bool> PlayAgainCondition;
        /// <summary>条件不满足时 WarnTip 显示的多语言 Key（如「体力不足」）。</summary>
        public string PlayAgainFailTipKey;
        /// <summary>点击「再来一局」回调（关闭面板由回调自行决定）。</summary>
        public Action OnPlayAgain;
        /// <summary>点击「返回」回调；为空时默认关闭本面板。</summary>
        public Action OnBack;
    }

    public override void Init()
    {
        playAgainButton.onClick.AddListener(OnPlayAgainButton);
        backButton.onClick.AddListener(OnBackButton);
    }

    /// <summary>填充并刷新结算面板。需在 OpenUI 之后调用。</summary>
    public void Show(Data data)
    {
        curData = data;
        string table = curData.Table;

        // 头像
        bool hasAvatar = curData.Avatar != null;
        if(hasAvatar)
            avatarImage.sprite = curData.Avatar;

        // 标题（可选）
        if(!string.IsNullOrEmpty(curData.TitleKey))
            titleText.SetTextSafe(table, curData.TitleKey);

        // 台词
        speechText.SetTextSafe(table, curData.SpeechKey);

        // 中间内容（含占位符）
        contentText.SetTextWithVars(table, curData.ContentKey, curData.ContentVars);

        // 获得物品提示（可隐藏）
        bool hasHint = !string.IsNullOrEmpty(curData.ItemHintKey);
        itemHintRoot.SetActive(hasHint);
        if(hasHint)
            itemHintText.SetTextSafe(table, curData.ItemHintKey);

        // 再来一局消耗体力
        bool showCost = curData.PlayAgainSpCost > 0;
        playAgainCostText.gameObject.SetActive(showCost);
        if(showCost)
            playAgainCostText.SetTextWithVars(table, LocVarSet.WitchPotion.SettlePlayAgainCost,
                (LocVarSet.CasinoSettle.Sp, curData.PlayAgainSpCost));

        playAgainButton.interactable = curData.PlayAgainInteractable;
    }

    #region 按钮
    void OnPlayAgainButton()
    {
        if(curData == null)
            return;

        // 条件不满足：弹 WarnTip 并拦截，不触发再来一局回调
        if(curData.PlayAgainCondition != null && !curData.PlayAgainCondition())
        {
            if(!string.IsNullOrEmpty(curData.PlayAgainFailTipKey))
                warnTip.Show(curData.Table, curData.PlayAgainFailTipKey);
            return;
        }

        curData.OnPlayAgain?.Invoke();
    }

    void OnBackButton()
    {
        curData?.OnBack?.Invoke();
        Close();
    }
    #endregion
}
