using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>什么时候起这段引导。</summary>
    public enum TutorialTriggerType
    {
        [LabelText("手动触发")]
        Manual = 0,

        [LabelText("进入场景")]
        EnterScene = 1,

        [LabelText("打开界面")]
        OpenUI = 2,

        [LabelText("剧情播完")]
        DramaFinish = 3,

        [LabelText("自定义事件")]
        Event = 4,
    }

    /// <summary>播过一次之后还播不播。</summary>
    public enum TutorialRepeatType
    {
        [LabelText("本存档一次")]
        Once = 0,

        [LabelText("跨存档一次")]
        OnceGlobal = 1,

        [LabelText("每次都播(调试用)")]
        Always = 2,
    }

    /// <summary>这一步要高亮谁。</summary>
    public enum TutorialTargetType
    {
        [LabelText("无目标(纯提示)")]
        None = 0,

        [LabelText("界面节点")]
        UIPath = 1,

        [LabelText("运行时锚点")]
        Anchor = 2,

        /// <summary>
        /// 直接在屏幕上圈一块。场景里的东西（Q版小人、场景物件）不是 UI 节点，
        /// 挂锚点组件太绕，位置又基本固定，直接把洞摆好就行。
        /// </summary>
        [LabelText("屏幕固定位置")]
        ScreenRect = 3,
    }

    /// <summary>洞的大小怎么定。</summary>
    public enum TutorialMaskFitType
    {
        [LabelText("贴合目标")]
        FitTarget = 0,

        [LabelText("自定义尺寸")]
        Custom = 1,
    }

    /// <summary>这一步怎么算过。</summary>
    public enum TutorialFinishType
    {
        [LabelText("点击目标")]
        ClickTarget = 0,

        [LabelText("点击任意处")]
        AnyClick = 1,

        /// <summary>
        /// 点在洞里就算过。判定只看点击位置，<b>不吃掉这一下点击</b> ——
        /// 所以玩家点场景小人时，小人照常被点到，引导同时往下走。
        /// 目标不是 UI 节点（点击报不上来）时用这个。
        /// </summary>
        [LabelText("点击洞内区域")]
        ClickHole = 5,

        [LabelText("等界面打开")]
        UIOpen = 2,

        [LabelText("等自定义事件")]
        Event = 3,

        [LabelText("延时")]
        Delay = 4,
    }

    /// <summary>气泡摆在洞的哪一侧。</summary>
    public enum TutorialTipPos
    {
        [LabelText("自动")]
        Auto = 0,

        [LabelText("上方")]
        Up = 1,

        [LabelText("下方")]
        Down = 2,

        [LabelText("左侧")]
        Left = 3,

        [LabelText("右侧")]
        Right = 4,
    }

    /// <summary>指引图标用哪个。</summary>
    public enum TutorialHandType
    {
        [LabelText("不显示")]
        None = 0,

        [LabelText("手指")]
        Finger = 1,

        [LabelText("箭头")]
        Arrow = 2,
    }
}
