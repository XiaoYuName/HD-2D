using System;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// UI适配Layer层级
    /// </summary>
    public enum UICanvasLayer
    {
        UIDown = 0,
        UIPanel = 1,
        UIPop = 2,
        UITop = 3,
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

    public enum PhotoLabelType
    {
        [LabelText("事件CG")]
        ActionCG = 0,
        [LabelText("HCG")]
        HCG = 1,
        [LabelText("照片")]
        Photograph = 2,
    }
    
    
    public enum ShowingModel
    {
        [LabelText("固定")]
        Fixed = 0,
        [LabelText("自定义")]
        Custom = 1,
    }

    public enum PropertyType
    {
        [LabelText("心情")]
        Feeling = 0,
        [LabelText("好感")]
        Goodwill = 1,
    }
    
    
    [Flags]
    public enum ShowingWeek
    {
        [LabelText("周一")]
        Monday = 1,
        [LabelText("周二")]
        Tuesday = 1 << 2,
        [LabelText("周三")]
        Wednesday = 1 << 3,
        [LabelText("周四")]
        Thursday = 1 << 4,
        [LabelText("周五")]
        Friday = 1 << 5,
        [LabelText("周六")]
        Saturday = 1 << 6,
        [LabelText("周日")]
        Sunday = 1 << 7
    }

    [Flags]
    public enum ShowingTime
    {
        [LabelText("早上")]
        Morning = 1,
        [LabelText("中午")]
        Noon = 1 << 1,
        [LabelText("傍晚")]
        Evening = 1 << 2,
        [LabelText("半夜")]
        Midnight = 1 << 3
    }

    [Flags]
    public enum FunctionType
    {
        [LabelText("对话")]
        Dialogue = 1,
        [LabelText("约会")]
        Dating = 1 << 1,
        [LabelText("送礼")]
        GiftGiving = 1 << 2,
        [LabelText("赌场(爆点冲刺)")]
        CasinoGame = 1 << 3,
        [LabelText("厨房")]
        Kitchen = 1 << 4,
        [LabelText("工厂")]
        Factory = 1 << 5,
        [LabelText("赌场(女巫毒药)")]
        CasinoGame_1 = 1 << 6,
    }

    public enum ItemSortType
    {
        CreatTime = 0,
        Number =  1,
        Quality = 2,
    }
}
