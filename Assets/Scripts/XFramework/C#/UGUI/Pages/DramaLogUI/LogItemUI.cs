using System.Collections;
using Drama.Runtime;
using Drama.Runtime.Services;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 对话记录里的一条。
///
/// <b>名字和正文都是走 <c>LocalizeStringEvent</c> 绑引用</b>，不是塞查好的字符串 ——
/// 和台词框一个口径：记录界面会一直挂在屏幕上，玩家这期间去设置里切语言，条目要跟着变。
///
/// <b>条目对象是走对象池复用的</b>，所以每次 <see cref="SetData"/> 都要把上一条留下的东西
/// （监听器、染上的颜色）覆盖干净，不能只在"这条有"的时候设。
/// </summary>
public class LogItemUI : UIBase
{
    private GameObject NameLabel;
    private LocalizeStringEvent nameStringEvent;
    private LocalizeStringEvent contentStingEvent;
    private HorizontalLayoutGroup nameLayoutGroup;
    private ContentSizeFitter contentSizeFitter;

    /// <summary>小喇叭。这条没配语音就整个收起来（原工程同样是没语音就什么都不做）。</summary>
    private Button voiceButton;

    private TMP_Text nameText;

    /// <summary>预制体上配的名字颜色。剧本没指定颜色时要回到它。</summary>
    private Color defaultNameColor = Color.white;

    /// <summary>
    /// 原始颜色只在<b>第一次</b>抓。
    /// <see cref="Init"/> 每次从池子里取出来都会跑一遍，不挡住的话第二次抓到的
    /// 是上一条台词染上去的颜色，"默认色"就一路漂下去了。
    /// </summary>
    private bool defaultNameColorCaptured;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        NameLabel = Get("NameLabel");
        nameStringEvent = Get<LocalizeStringEvent>("NameLabel/Label");
        contentStingEvent = Get<LocalizeStringEvent>("ContentLabel");
        nameLayoutGroup = Get<HorizontalLayoutGroup>("NameLabel");
        contentSizeFitter = Get<ContentSizeFitter>("NameLabel");
        voiceButton = Get<Button>("NameLabel/BtnVoice");

        // 名字文本和 LocalizeStringEvent 在同一个节点上
        nameText = nameStringEvent != null ? nameStringEvent.GetComponent<TMP_Text>() : null;

        if (!defaultNameColorCaptured && nameText != null)
        {
            defaultNameColor = nameText.color;
            defaultNameColorCaptured = true;
        }
    }

    /// <summary>
    /// 摆一条台词。
    ///
    /// 名字走 <see cref="DramaSpeakerName"/> 的四路寻址（旁白 / 主角 / 自定义 / 指定角色），
    /// 和对话框上的名字栏是同一份逻辑 —— 旁白和没配名字的角色一律把名字栏整个收掉，
    /// 而不是显示一个空框，原工程也是这么做的（<c>TalkNameBG.SetActive(false)</c>）。
    ///
    /// ⚠ 小喇叭是名字栏的子节点，所以<b>旁白的语音重播不了</b>（名字栏一收它跟着没）。
    /// 原工程的 BtnVoice 同样长在 NameBg 下面，行为一致。要让旁白也能重播，
    /// 得把按钮挪到名字栏外面。
    /// </summary>
    public void SetData(DialogueLine line)
    {
        bool hasName = DramaSpeakerName.TryResolve(line, out string nameTable, out string nameKey);

        NameLabel.SetActive(hasName);

        if (hasName)
        {
            nameStringEvent.SetText(nameTable, nameKey);
            ApplyNameColor(line.NameColor);
        }

        contentStingEvent.SetText(line.TextRef.Table, line.TextRef.Key);

        BindVoice(line.VoiceRef);

        // 名字栏的宽度要等文字实际排完才算得出来，只能等一帧
        StartCoroutine(RelayoutName(hasName));
    }

    /// <summary>
    /// 上名字颜色。规则和对话框一致（见 <c>TalkNameController.ApplyNameColor</c>）：
    /// <b>白色 = 剧本没指定</b>，回到预制体上配的颜色；不是"跳过不设"——
    /// 跳过的话池子里上一条的自定义颜色会留在这一条上。
    /// </summary>
    private void ApplyNameColor(Color scriptColor)
    {
        if (nameText == null)
        {
            return;
        }

        nameText.color = scriptColor == Color.white ? defaultNameColor : scriptColor;
    }

    /// <summary>
    /// 接小喇叭。没配语音的条目直接把按钮收掉 ——
    /// 原工程是点了发现没语音就什么都不做，按钮一直亮着更容易让玩家以为是坏了。
    /// </summary>
    private void BindVoice(LocalizedRef voice)
    {
        if (voiceButton == null)
        {
            return;
        }

        bool hasVoice = !voice.IsEmpty;
        voiceButton.gameObject.SetActive(hasVoice);

        // ★ 池子里取出来的对象带着上一条的监听器，不摘干净会连着上一条的语音一起播
        voiceButton.onClick.RemoveAllListeners();

        if (!hasVoice)
        {
            return;
        }

        LocalizedRef captured = voice;
        voiceButton.onClick.AddListener(() => DramaManager.Instance.PlayHistoryVoice(captured));
    }

    /// <summary>
    /// 名字栏是"跟着字宽"的（HorizontalLayoutGroup + ContentSizeFitter），
    /// 而多语言那边刚 <c>RefreshString</c>，这一帧文字还没排进去 ——
    /// 立刻算宽度会拿到上一条的（池子复用）或者 0。等到帧末再手动踢一下。
    /// </summary>
    private IEnumerator RelayoutName(bool hasName)
    {
        yield return new WaitForEndOfFrame();

        if (!hasName || contentSizeFitter == null || nameLayoutGroup == null)
        {
            yield break;
        }

        contentSizeFitter.SetLayoutHorizontal();
        nameLayoutGroup.CalculateLayoutInputHorizontal();
    }
}
