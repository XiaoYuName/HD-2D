using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 一步引导的运行时上下文：<see cref="TutorialManager"/> 把配置和解析出来的目标打包给
    /// <see cref="TutorialUI"/>，界面只按它画，不认识配置资产也不认识业务界面。
    /// </summary>
    public sealed class TutorialStepContext
    {
        /// <summary>这一步的配置。</summary>
        public TutorialStepConfig Data;

        /// <summary>
        /// 要高亮的目标节点。<see cref="TutorialTargetType.None"/> 的步骤是 null —— 那种步骤
        /// 只画一层全屏遮罩加提示文字，不挖洞。
        /// </summary>
        public RectTransform Target;

        /// <summary>
        /// 洞的形状图。null 表示用 TutorialUI 预制体上 Unmask 自带的默认方形九宫格。
        /// 人物立绘这类异形洞就是靠它 —— Unmask 走的是 StencilOp.Invert + UNITY_UI_ALPHACLIP，
        /// 洞的形状跟着这张图的 alpha 走，不是只能挖矩形。
        /// </summary>
        public Sprite MaskSprite => Data?.MaskSprite;

        /// <summary>洞内点击是否透传给目标。false 时整屏都点不动（纯展示步骤）。</summary>
        public bool ClickThrough => Data == null || Data.ClickThrough;
    }
}
