using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// UI适配Layer层级
    /// </summary>
    public enum UICanvasLayer
    {
        UIDown = 0,
        UIPanel = 2,
        UIPop = 4,
        UITop = 6,
    }

    /// <summary>
    /// UI子层级Layer
    /// </summary>
    public enum UIParentLayer
    {
        UIPanel = 0,
        UIDialogue = 1,
        UIPop = 2,
        UITop = 3,
    }

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

    public enum HotUpdateState
    {
        Node = 0,
        UseButton = 1,
        Download = 2,
        WaitGame = 3,
    }

    public enum GameSettingType
    {
        /// <summary>
        /// 游戏设置
        /// </summary>
        [LabelText("游戏设置")]
        GameSettings = 0,
        /// <summary>
        /// 显示设置
        /// </summary>
        [LabelText("显示设置")]
        DisplaySettings = 1,
        /// <summary>
        /// 声音设置
        /// </summary>
        [LabelText("声音设置")]
        AudioSettings = 2,
        /// <summary>
        /// 其他啊设置
        /// </summary>
        [LabelText("其他设置")]
        OtherSettings = 3,
    }
}
