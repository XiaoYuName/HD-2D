using UnityEngine;
using XFramework;

/// <summary>
/// 新手引导遮罩界面：只负责把 <see cref="TutorialStepContext"/> 画出来 ——
/// 挖洞(Unmask)、气泡提示、手指图标。判断"该教哪一步、什么时候算过"都在
/// <see cref="TutorialManager"/> 里，这里不碰配置表也不认识业务界面。
/// </summary>
public class TutorialUI : UIBase
{
    /// <summary>当前正在展示的步骤，表现层内部自己用。</summary>
    private TutorialStepContext context;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        // TODO(引导表现层): 绑定 Mask/Unmask/UnmaskRaycastFilter/气泡/手指节点
    }

    /// <summary>
    /// 展示一步引导。<see cref="TutorialManager"/> 调，参数里已经带好了解析完的目标节点和洞形状图。
    /// </summary>
    public void ShowStep(TutorialStepContext stepContext)
    {
        context = stepContext;

        // TODO(引导表现层): 按 context 设置 Unmask.fitTarget / 洞的 sprite 与扩边 /
        // UnmaskRaycastFilter 的启停(ClickThrough) / 气泡文本与位置 / 手指动画。
        // AnyClick 的步骤要在遮罩上收点击并回调 TutorialManager.Instance.NotifyMaskClick()。
    }

    /// <summary>收摊：把洞、气泡、手指都收掉。引导完成或中止时调。</summary>
    public void Clear()
    {
        context = null;

        // TODO(引导表现层): 复位 Unmask 与气泡/手指的显示状态
    }

    public override void Close()
    {
        Clear();
        base.Close();
    }
}
