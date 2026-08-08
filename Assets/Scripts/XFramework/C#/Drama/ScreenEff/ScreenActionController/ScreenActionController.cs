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
        
    }

    /// <summary>揭开遮罩（画面恢复）。跑完遮罩应当完全透明且不吃点击。</summary>
    public async UniTask RevealAsync(EScreenTransitionKind kind, float seconds, Ease ease, CancellationToken ct)
    {
        
    }

    /// <summary>
    /// 立刻把遮罩清干净。剧本结束 / 跳转 / 被打断时调 ——
    /// 剧本可能正停在「盖着黑幕」的状态，不清就是一块黑屏卡在玩家脸上。
    /// </summary>
    public void Clear()
    {
        
    }
}
