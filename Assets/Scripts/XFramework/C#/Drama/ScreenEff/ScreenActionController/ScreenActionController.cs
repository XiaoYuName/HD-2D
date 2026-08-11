using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime;
using Drama.Runtime.Services;
using UnityEngine;
using XFramework;

public partial class ScreenActionController : UIBase,IDramaScreen
{
    public override void Init()
    {
        InitAutoBind();
        fadeController.Init();
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    /// <summary>
    /// 盖上遮罩（画面转黑 / 转白）。
    ///
    /// <b>跑完停在 <paramref name="alpha"/> 上，不要自动还原</b> ——
    /// 剧本经常是「盖上 → 换背景换立绘 → 揭开」，中间那几条指令就指望遮罩一直挡着。
    /// </summary>
    public async UniTask CoverAsync(EScreenTransitionKind kind, float seconds, Color color, float alpha, Ease ease,
        CancellationToken ct)
    {
        switch (kind)
        {
            case EScreenTransitionKind.Fade:
                await fadeController.FadeIn(seconds, color, alpha, ease, ct);
                break;
            case EScreenTransitionKind.VenetianBlind:
            case EScreenTransitionKind.Comb:
                // 两种条纹共用一张遮罩 Image + 一个 Shader，样式由 kind 决定
                await fadeController.WipeIn(kind, seconds, color, alpha, ease, ct);
                break;
        }
    }

    /// <summary>揭开遮罩（画面恢复）。跑完遮罩应当完全透明且不吃点击。</summary>
    public async UniTask RevealAsync(EScreenTransitionKind kind, float seconds, Ease ease, CancellationToken ct)
    {
        switch (kind)
        {
            case EScreenTransitionKind.Fade:
                await fadeController.FadeOut(seconds,ease, ct);
                break;
            case EScreenTransitionKind.VenetianBlind:
            case EScreenTransitionKind.Comb:
                await fadeController.WipeOut(kind, seconds, ease, ct);
                break;
        }
    }

    /// <summary>
    /// 立刻把遮罩清干净。剧本结束 / 跳转 / 被打断时调 ——
    /// 剧本可能正停在「盖着黑幕」的状态，不清就是一块黑屏卡在玩家脸上。
    /// </summary>
    /// <summary>把正在跑的转场立刻推到终点。切到跳过模式时用，语义见 FadeController 里的注释。</summary>
    public void CompleteRunning()
    {
        fadeController.CompleteImmediate();
    }

    public void Clear()
    {
        // 条纹过场被中途取消时，材质会停在半覆盖上 —— 不清就是一屏黑条卡在玩家脸上，
        // 比停在纯黑上还难看。ClearImmediate 掐 Tween + 摘材质 + alpha 归零，都做了
        fadeController.ClearImmediate();
    }
}
