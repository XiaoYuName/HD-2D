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
        /// 其他设置
        /// </summary>
        [LabelText("其他设置")]
        OtherSettings = 3,
    }

    public enum PhotoLabelType
    {
        [LabelText("事件CG")]
        ActionCG = 0,
        [LabelText("HCG")]
        HCG = 1,
        [LabelText("照片")]
        Photograph = 2,
    }

    public enum ItemSortType
    {
        CreatTime = 0,
        Number =  1,
        Quality = 2,
    }

    
    public enum ShopMode
    {
        [LabelText("购买界面")]
        Buy = 0,
        [LabelText("出售界面")]
        Sell = 1,
    }

    public enum StateType
    {
        None = 0,
        Lock = 1,
        Unlock = 2,
    }

    public enum MinGameSceneType
    {
        /// <summary>
        /// 娃娃机游戏场景
        /// </summary>
        ClawMachineScene = 0,
    }
    
    public enum OnLinePageType
    {
        /// <summary>
        /// 无
        /// </summary>
        [LabelText("Node")]
        None = 0,
        /// <summary>
        /// 粉丝页签
        /// </summary>
        [LabelText("粉丝")]
        Fan = 1,
        [LabelText("发布动态")]
        PostingUpdates = 2,
    }
}
