namespace XFramework
{
    /// <summary>
    /// **累计几次**型目标（打完几局、送几次礼、买几件）：世界状态里查不到「你打过几局」，
    /// 只能自己记，所以次数存在 <see cref="CountObjInfo.count"/> 里，只增不减。
    /// </summary>
    public abstract class CountObjData : QuestObjData
    {
        /// <summary>要累计到的次数。</summary>
        public abstract int Need { get; }

        protected override void SetTemplateVars(LocVars vars)
        {
            vars.Set(QuestLocVar.Value, Need);
            base.SetTemplateVars(vars);
        }
    }
}
