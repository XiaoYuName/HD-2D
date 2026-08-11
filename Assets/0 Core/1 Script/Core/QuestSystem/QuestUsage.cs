namespace XFramework
{
    /// <summary>
    /// 一种类型的正确写法：最少几个参数 + 打给策划看的示例。
    /// 各实现的 <c>Init</c> 只管读参数，校验统一由 <see cref="QuestConfigValidator.ValidateUsage"/> 在启动时做一遍。
    /// </summary>
    public readonly struct QuestUsage
    {
        /// <summary>最少要几个参数（类型名之后的段数）。</summary>
        public readonly int LeastArgs;

        /// <summary>正确写法示例，<c>[]</c> 内表示可省略。</summary>
        public readonly string Text;

        public QuestUsage(int leastArgs, string text)
        {
            LeastArgs = leastArgs;
            Text = text;
        }

        public override string ToString() => Text;
    }
}
