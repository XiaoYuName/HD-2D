namespace TestSystem
{
    using XFramework;

    /// <summary>刺绣模拟小游戏测试分类。</summary>
    public sealed class DressMakingEmbroiderySimulationGameTestCategory : ITestCategory
    {
        const string CategoryTitle = "刺绣模拟小游戏";
        const string OpenLabel = "打开（10001 示例）";
        const long DefaultClothingId = 10001;

        public string Title => CategoryTitle;

        public void SetActions(TestActionList actionList)
        {
            actionList.Add(OpenLabel, Open);
        }

        static void Open()
        {
            UISystem.Instance.OpenUI<DressMakingEmbroiderySimulationGamePanel>(nameof(DressMakingEmbroiderySimulationGamePanel)).SetClothing(DefaultClothingId);
        }
    }
}
