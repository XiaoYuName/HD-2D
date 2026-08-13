namespace XFramework
{
    /// <summary>
    /// 任务配置的加载方式。要换资源管线（AA、AB、本地直读…）就另写一个实现，
    /// 用 <see cref="QuestConfigProvider.SetLoader"/> 装进去；用配置的地方一律走
    /// <see cref="QuestConfigProvider.Config"/>，不认加载细节。
    /// </summary>
    public interface IQuestConfigLoader
    {
        QuestConfig Load();
    }
}
