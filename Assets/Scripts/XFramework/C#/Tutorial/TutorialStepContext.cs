using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 一步引导的运行时上下文：<see cref="TutorialManager"/> 把配置解析成这个对象，
    /// <see cref="TutorialUI"/> 只按它画界面，不认识配置表也不认识业务界面。
    ///
    /// 业务界面（大厅、背包……）因此完全不用为引导写代码 —— 目标节点是引导自己找出来的。
    /// </summary>
    public sealed class TutorialStepContext
    {
        /// <summary>原始配置行。表现层要读 TipTextKey / HandType / MaskPadding 这些都从这里拿。</summary>
        public TutorialStepData Data;

        /// <summary>
        /// 要高亮的目标节点。<see cref="TutorialTargetType.None"/> 的步骤是 null —— 那种步骤
        /// 只画一层全屏遮罩加提示文字，不挖洞。
        /// </summary>
        public RectTransform Target;

        /// <summary>
        /// 洞的形状图。null 表示用 TutorialUI 预制体上 Unmask 自带的默认方形九宫格。
        /// 人物立绘这类异形洞就是靠它 —— Unmask 走的是 StencilOp.Invert + UNITY_UI_ALPHACLIP，
        /// 洞的形状跟着这张图的 alpha 走，不是只能挖矩形。
        /// 资源由 <see cref="TutorialManager"/> 负责加载和释放，表现层直接用就行。
        /// </summary>
        public Sprite MaskSprite;

        /// <summary>
        /// 洞内点击是否透传给目标。false 时整屏都点不动（纯展示步骤），
        /// 靠 UnmaskRaycastFilter 的启停实现。
        /// </summary>
        public bool ClickThrough => Data == null || Data.ClickThrough;

        /// <summary>遮罩上任意位置点一下就能过场。表现层据此决定遮罩要不要吃点击。</summary>
        public bool FinishOnAnyClick => Data != null && Data.FinishType == TutorialFinishType.AnyClick;
    }
}
