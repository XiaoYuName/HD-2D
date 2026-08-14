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

    // ============================================================ AUTO / SKIP / LOG

    // 按钮长在这一层（它们是对话框皮肤的一部分），但"点了之后怎么推进剧情"归
    // TalkActionController —— 那边才知道打字机在不在跑、这句翻没翻页。
    // 所以这里只负责接线和把选中态画出来。
    //
    // ★ 两套皮肤各有一整排按钮（普通框 Memu / CG 框 HCGMemu），和原工程一致
    //   （那边是 mBtnMenuObj 和 mBtnCGMenuObj 各绑一次，回调是同一个）。
    //   两排接的是同一批回调、刷的是同一个状态 —— 玩家眼里就是"同一个按钮换了张皮"，
    //   切皮肤时不能出现"普通框开着自动、CG 框显示没开"。

    /// <summary>把 AUTO / SKIP / LOG 的点击接出去。<see cref="TalkActionController"/> 在 Init 里调。</summary>
    public void BindPlaybackButtons(UnityAction onAuto, UnityAction onSkip, UnityAction onLog)
    {
        BindButton(autoButton, onAuto);
        BindButton(hcgAutoButton, onAuto);

        BindButton(skipButton, onSkip);
        BindButton(hcgSkipButton, onSkip);

        BindButton(logButton, onLog);
        BindButton(hcgLogButton, onLog);
    }

    /// <summary>
    /// 按当前播放模式刷选中态，两套皮肤一起刷。AUTO / SKIP 互斥，不会同时亮。
    ///
    /// LOG 没有选中态 —— 它是"开一个界面"，不是一个持续的模式。
    /// </summary>
    public void RefreshPlaybackButtons(EDramaPlaybackMode mode)
    {
        bool auto = mode == EDramaPlaybackMode.Auto;
        bool skip = mode == EDramaPlaybackMode.Skip;

        autoButton?.SetSelected(auto);
        hcgAutoButton?.SetSelected(auto);

        skipButton?.SetSelected(skip);
        hcgSkipButton?.SetSelected(skip);
    }

    /// <summary>预制体上少一个按钮不该炸，接不上就算了。</summary>
    private static void BindButton(SelectedButton button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
}
