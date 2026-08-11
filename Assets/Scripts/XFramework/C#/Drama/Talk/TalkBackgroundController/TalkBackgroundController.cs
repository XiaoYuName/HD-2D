using Drama.Runtime;
using Drama.Runtime.Flow;
using UnityEngine.Events;
using XFramework;

public partial class TalkBackgroundController : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetFrame(ETalkFrame frame)
    {
        talkNormalBackgroud.gameObject.SetActive(frame == ETalkFrame.Normal);
        talkHCGBackgroud.gameObject.SetActive(frame == ETalkFrame.HCG);
    }

    // ============================================================ AUTO / SKIP

    // 按钮长在这一层（它俩是对话框皮肤的一部分），但"点了之后怎么推进剧情"归
    // TalkActionController —— 那边才知道打字机在不在跑、这句翻没翻页。
    // 所以这里只负责接线和把选中态画出来。

    /// <summary>把 AUTO / SKIP 的点击接出去。<see cref="TalkActionController"/> 在 Init 里调。</summary>
    public void BindPlaybackButtons(UnityAction onAuto, UnityAction onSkip)
    {
        if (autoButton != null)
        {
            autoButton.onClick.RemoveAllListeners();
            autoButton.onClick.AddListener(onAuto);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(onSkip);
        }
    }

    /// <summary>按当前播放模式刷两个按钮的选中态。两者互斥，不会同时亮。</summary>
    public void RefreshPlaybackButtons(EDramaPlaybackMode mode)
    {
        autoButton?.SetSelected(mode == EDramaPlaybackMode.Auto);
        skipButton?.SetSelected(mode == EDramaPlaybackMode.Skip);
    }
}
