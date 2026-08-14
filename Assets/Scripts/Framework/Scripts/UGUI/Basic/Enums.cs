using System;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 回调参数的回调时机
    /// </summary>
    public enum ActionBehaviour
    {
        /// <summary>
        /// 一开始调用
        /// </summary>
        Star,
        /// <summary>
        /// 中途回调
        /// </summary>
        Mid,
        /// <summary>
        /// 结束时回调执行
        /// </summary>
        End,
    }

    /// <summary>
    /// 转场渐变遮罩的遮挡范围。对应 Project Settings 里两个专门的 Sorting Layer。
    /// </summary>
    public enum FadeLayer
    {
        /// <summary>
        /// SceneFade:只遮住场景,UI照常显示(小场景之间切换用)
        /// </summary>
        Scene = 0,
        /// <summary>
        /// UIFade:最顶层,连UI一起遮掉(进出小游戏、读档这种整体转场用)
        /// </summary>
        All = 1,
    }
}
