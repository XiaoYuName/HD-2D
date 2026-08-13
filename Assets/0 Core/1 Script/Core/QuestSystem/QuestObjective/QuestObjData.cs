using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 一条目标的**静态数据**：参数、文案、校验规则。直接存在 <see cref="QuestConfig"/> 里（Odin 多态序列化），
    /// 引用同一条目标的多个任务共享同一个对象，所以这里不能存任何随玩家变化的东西 ——
    /// 进度和事件订阅都在 <see cref="QuestObjInfoBase"/>。
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
        /// 目标描述。文案里能用哪些占位符由目标类型决定：<see cref="QuestLocVar.Progress"/> 人人都有，
        /// <see cref="QuestLocVar.Value"/> 只有带量的两族有，其余的看各类的 <see cref="SetDescVars"/>。
        /// </summary>
        [SerializeField, QuestLabel("目标描述")] LocKeyRef desc = new();

        public LocKeyRef Desc => desc;

        /// <summary>文案配了没有。空的话 UI 上就是一片空白，所以启动校验时就要报出来。</summary>
        public bool HasDesc => desc.IsValid();

        /// <summary>
        /// 校验参数指向的东西真的存在，走 <see cref="QuestConfigValidator"/>。
        /// 只在 <see cref="QuestManager"/> 初始化阶段查一次，所以运行时不再做空判兜底。
        /// </summary>
        public abstract bool Validate(string owner);

        /// <summary>造一份带自己进度的运行时实例，返回本类的嵌套 <c>Info</c>。绑定由 <see cref="QuestObjStateInfo.Set"/> 完成。</summary>
        public abstract QuestObjInfoBase CreateInfo();

        /// <summary>静态文案模板 ＋ 动态进度，拼成「持有 鱼 ×2（1/2）」。进度由动态侧算好传进来。</summary>
        public string GetDesc(string progressText)
        {
            if (!HasDesc) return string.Empty;

            // 占位符攒在 LocVars 里，取的时候才交给多语言层 —— desc 是整局共用的，不能把占位符写进去
            LocVars vars = new();
            vars.Set(QuestLocVar.Progress, progressText);
            SetTemplateVars(vars);
            return desc.Get(vars);
        }

        /// <summary>
        /// 族基类填本族共有的占位符（带量的族填 <see cref="QuestLocVar.Value"/>），
        /// 末尾必须接着调 <see cref="SetDescVars"/>，叶子类只管重写那一个。
        /// </summary>
        protected virtual void SetTemplateVars(LocVars vars) => SetDescVars(vars);

        /// <summary>把自己那几个占位符（道具名、角色名……）塞进文案。</summary>
        protected virtual void SetDescVars(LocVars vars) { }

        public override string ToString() => GetType().Name;
    }
}
