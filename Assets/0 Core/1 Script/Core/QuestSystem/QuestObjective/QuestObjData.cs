using UnityEngine.Localization;

namespace XFramework
{
    /// <summary>
    /// 一条目标的**静态数据**：解析好的参数、文案模板、校验规则。
    /// 读表时造一份（<see cref="QuestObjConfigData"/> 持有），整局共用 —— 引用同一条目标的多个任务共享同一个对象。
    /// 共用就意味着这里不能存任何随玩家变化的东西，进度和事件订阅都在 <see cref="QuestObjInfoBase"/>。
    ///
    /// 按「达成条件长什么样」分三族，各族的 <c>Need</c>／判定方法签名都不一样，不用一个 int 兼职布尔：
    /// <see cref="FlagObjData"/>（成没成）、<see cref="AmountObjData"/>（现在多少）、<see cref="CountObjData"/>（累计几次）。
    ///
    /// 每种目标把自己的运行时实例写成嵌套类 <c>Info</c>：嵌套类能直接读外层的私有参数，
    /// 于是事件回调是普通命名方法（<c>if (id == ObjData.npcId) Advance(1);</c>），
    /// 既不用把参数放大成 public，也不用闭包。
    /// </summary>
    public abstract class QuestObjData
    {
        /// <summary>
        /// 描述文案 Key，由目标表的 <c>DescKey</c> / <c>ExtraDescKey</c> 列注入（见 <see cref="QuestObjConfigData"/>）。
        /// 文案里能用哪些占位符由目标类型决定：<see cref="QuestLocVar.Progress"/> 人人都有，
        /// <see cref="QuestLocVar.Value"/> 只有带量的两族有，其余的看各类的 <see cref="SetDescVars"/>。
        /// </summary>
        public string DescKey { get; set; }

        /// <summary>测试用：代码里临时换文案，非空时盖掉 <see cref="DescKey"/>。</summary>
        public string DescKeyOverride { get; set; }

        string UsedDescKey => string.IsNullOrEmpty(DescKeyOverride) ? DescKey : DescKeyOverride;

        /// <summary>文案配了没有。空的话 UI 上就是一片空白，所以在读表阶段就要报出来。</summary>
        public bool HasDescKey => !string.IsNullOrEmpty(UsedDescKey);

        /// <summary>读参数并校验写法（段数、是不是数字），一条配置只在读表时调一次。</summary>
        public abstract void Init(QuestArgs config);

        /// <summary>
        /// 校验参数指向的东西真的存在，走 <see cref="QuestConfigValidator"/>。
        /// 只在 <see cref="QuestManager"/> 初始化阶段查一次，所以运行时不再做空判兜底。
        /// </summary>
        public abstract bool Validate(QuestArgs config);

        /// <summary>造一份带自己进度的运行时实例，返回本类的嵌套 <c>Info</c>。绑定由 <see cref="QuestObjStateInfo.Set"/> 完成。</summary>
        public abstract QuestObjInfoBase CreateInfo();

        /// <summary>静态文案模板 ＋ 动态进度，拼成「持有 鱼 ×2（1/2）」。进度由动态侧算好传进来。</summary>
        public string GetDesc(string progressText)
        {
            LocalizedString desc = new(LocTableSet.QuestSystem, UsedDescKey);
            desc.SetVar(QuestLocVar.Progress, progressText, false);
            SetTemplateVars(desc);
            return desc.GetLocalizedString();
        }

        /// <summary>
        /// 族基类填本族共有的占位符（带量的族填 <see cref="QuestLocVar.Value"/>），
        /// 末尾必须接着调 <see cref="SetDescVars"/>，叶子类只管重写那一个。
        /// </summary>
        protected virtual void SetTemplateVars(LocalizedString desc) => SetDescVars(desc);

        /// <summary>把自己那几个占位符（道具名、角色名……）塞进文案。</summary>
        protected virtual void SetDescVars(LocalizedString desc) { }

        public override string ToString() => GetType().Name;
    }
}
