using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Flow;
using Drama.Runtime.Services;
using Febucci.UI;
using TMPro;
using UnityEngine.Localization.Components;
using XFramework;

public partial class TalkContentController : UIBase
{
    /// <summary>快进时打字机的倍率。<c>SetTypewriterSpeed</c> 是倍率，值越大越快。</summary>
    private const float FastForwardSpeed = 4f;

    private ETalkFrame eTalkFarme;
    private TypewriterByCharacter normalTypewrite;
    private TypewriterByCharacter hCGTypewrite;

    /// <summary>
    /// 当前这句打字机跑完的信号。
    ///
    /// <b>不轮询 isShowingText</b> —— SetText 之后打字机不一定在同一帧就置位，
    /// 轮询会在第一帧就看到 false 直接放过去（台词一闪而过）；
    /// 反过来空文本时又可能永远等不到。所以只认 onTextShowed 事件。
    /// </summary>
    private UniTaskCompletionSource textShown;

    private TypewriterByCharacter CurrentTypewriter =>
        eTalkFarme == ETalkFrame.HCG ? hCGTypewrite : normalTypewrite;

    private LocalizeStringEvent CurrentContext =>
        eTalkFarme == ETalkFrame.HCG ? talkHCGContext : talkNormalContext;

    /// <summary>打字机是不是还在逐字显示。点击三态机要看它。</summary>
    public bool IsShowingText => CurrentTypewriter.isShowingText;

    /// <summary>
    /// 当前这句台词的字数。自动播放要按它算等待时长（照抄原工程：0.075 × 字数 + 0.3 秒）。
    ///
    /// 取的是 TMP 上的实际文本而不是剧本里的 key —— 多语言换一种语言字数就变了，
    /// 而"念完这句要多久"跟的是玩家眼前看到的那串字。
    /// </summary>
    public int CurrentTextLength => CurrentLabel != null ? CurrentLabel.text.Length : 0;

    private TMP_Text normalLabel;
    private TMP_Text hCGLabel;

    private TMP_Text CurrentLabel =>
        eTalkFarme == ETalkFrame.HCG ? hCGLabel : normalLabel;

    public override void Init()
    {
        InitAutoBind();

        normalTypewrite = talkNormalContext.GetComponent<TypewriterByCharacter>();
        hCGTypewrite = talkHCGContext.GetComponent<TypewriterByCharacter>();

        normalLabel = talkNormalContext.GetComponent<TMP_Text>();
        hCGLabel = talkHCGContext.GetComponent<TMP_Text>();

        // SkipTypewriter() 和自然跑完都会走 onTextShowed，两条出口在这里合并成一个
        normalTypewrite.onTextShowed.AddListener(OnTextShowed);
        hCGTypewrite.onTextShowed.AddListener(OnTextShowed);
    }

    /// <summary>
    /// 掐掉还在 await 的 <see cref="ShowTextAsync"/>，<b>但不隐藏自己</b>。
    ///
    /// 和 <see cref="Close"/> 分开是有原因的：父级收对话框时只需要"别把等待方挂死"，
    /// 顺手把本节点 SetActive(false) 的话，父级下次 Open() 只开自己、开不到子节点，
    /// 打字机就永远不显示了。
    /// </summary>
    public void AbortWaiting()
    {
        textShown?.TrySetCanceled();
        textShown = null;
    }

    public override void Close()
    {
        AbortWaiting();
        base.Close();
    }

    public void SetFrame(ETalkFrame frame)
    {
        eTalkFarme = frame;
        normal.gameObject.SetActive(frame == ETalkFrame.Normal);
        hCG.gameObject.SetActive(frame == ETalkFrame.HCG);
    }

    /// <summary>把打字机一次性推到全文。玩家点击打断时用。</summary>
    public void SkipTypewriter()
    {
        CurrentTypewriter.SkipTypewriter();
    }

    /// <summary>
    /// 显示一句台词，等打字机跑完（或被 <see cref="SkipTypewriter"/> 打断）。
    ///
    /// 文本走 <c>LocalizeStringEvent.SetText(table, key)</c> 而不是直接
    /// <c>ShowText(字符串)</c>：<b>绑定关系留着，玩家中途切语言时
    /// OnUpdateString 会把新文本重新喂给打字机</b>。
    /// </summary>
    public async UniTask ShowTextAsync(DialogueLine line, EDramaPlaybackMode mode, CancellationToken ct)
    {
        TypewriterByCharacter typewriter = CurrentTypewriter;

        typewriter.SetTypewriterSpeed(mode == EDramaPlaybackMode.FastForward ? FastForwardSpeed : 1f);

        textShown = new UniTaskCompletionSource();

        // SetText → RefreshString → OnUpdateString → 打字机 ShowText
        CurrentContext.SetText(line.TextRef.Table, line.TextRef.Key);

        if (mode == EDramaPlaybackMode.Skip)
        {
            // 跳过模式不放动画，但文本该显示还得显示（Log 里要留一条）
            typewriter.SkipTypewriter();
        }

        try
        {
            // 上面那些同步路径可能已经把 tcs 置完了，这时 await 直接过，不会卡
            await textShown.Task.AttachExternalCancellation(ct);
        }
        finally
        {
            textShown = null;
        }
    }

    private void OnTextShowed()
    {
        textShown?.TrySetResult();
    }
}
