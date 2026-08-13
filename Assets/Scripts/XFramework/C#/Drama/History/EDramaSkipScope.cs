using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 「跳过」的作用范围。设置界面的两个选项，读写走
    /// <c>DramaManager.SkipScope</c> / <c>DramaManager.SetSkipScope</c>。
    ///
    /// 存的是玩家的<b>全局偏好</b>（PlayerPrefs，和音量一个性质），不进存档 —— 换存档槽不该变。
    /// </summary>
    public enum EDramaSkipScope
    {
        /// <summary>
        /// 只跳已读（默认）。跳过时撞到没读过的台词就自动退出跳过，让那一句正常播。
        ///
        /// 判断"读过没有"走的是跨存档共享的已读记录，见 <see cref="DramaReadMarks"/>。
        /// </summary>
        [LabelText("只跳已读")]
        OnlyRead = 0,

        /// <summary>
        /// 全部跳过。不管读过没读过，一路跳到剧情结束（或者玩家自己点停）。
        ///
        /// 原工程没有这个选项：它是"整本没读完过就<b>不给点</b> SKIP"，比这个粗。
        /// </summary>
        [LabelText("全部跳过")]
        All = 1,
    }
}
