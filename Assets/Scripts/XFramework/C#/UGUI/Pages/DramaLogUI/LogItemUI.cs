using System.Collections;
using Drama.Runtime.Services;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 对话记录里的一条。
///
/// <b>名字和正文都是走 <c>LocalizeStringEvent</c> 绑引用</b>，不是塞查好的字符串 ——
/// 和台词框一个口径：记录界面会一直挂在屏幕上，玩家这期间去设置里切语言，条目要跟着变。
/// </summary>
public class LogItemUI : UIBase
{
    private GameObject NameLabel;
    private LocalizeStringEvent nameStringEvent;
    private LocalizeStringEvent contentStingEvent;
    private HorizontalLayoutGroup nameLayoutGroup;
    private ContentSizeFitter contentSizeFitter;

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
    }

    /// <summary>
    /// 摆一条台词。
    ///
    /// 名字走 <see cref="DramaSpeakerName"/> 的四路寻址（旁白 / 主角 / 自定义 / 指定角色），
    /// 和对话框上的名字栏是同一份逻辑 —— 旁白和没配名字的角色一律把名字栏整个收掉，
    /// 而不是显示一个空框，原工程也是这么做的（<c>TalkNameBG.SetActive(false)</c>）。
    /// </summary>
    public void SetData(DialogueLine line)
    {
        bool hasName = DramaSpeakerName.TryResolve(line, out string nameTable, out string nameKey);

        NameLabel.SetActive(hasName);

        if (hasName)
        {
            nameStringEvent.SetText(nameTable, nameKey);
        }

        contentStingEvent.SetText(line.TextRef.Table, line.TextRef.Key);

        // 名字栏的宽度要等文字实际排完才算得出来，只能等一帧
        StartCoroutine(RelayoutName(hasName));
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
