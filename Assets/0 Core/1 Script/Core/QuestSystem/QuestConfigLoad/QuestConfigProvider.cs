using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 配置的全局入口。<see cref="QuestManager"/> 启动时用自己身上的 AssetReference 装一个
    /// <see cref="QuestConfigAssetLoader"/> 进来；编辑器下没装就退到 <see cref="QuestConfigEditorLoader"/>。
    /// </summary>
    public static class QuestConfigProvider
    {
        static IQuestConfigLoader loader;
        static QuestConfig config;

        public static QuestConfig Config => config != null ? config : config = Load();

        /// <summary>换加载方式，同时丢掉已缓存的那一份。</summary>
        public static void SetLoader(IQuestConfigLoader value)
        {
            loader = value;
            config = null;
        }

        public static void ClearCache() => config = null;

        static QuestConfig Load()
        {
            if (loader != null) return loader.Load();

#if UNITY_EDITOR
            loader = new QuestConfigEditorLoader();
            return loader.Load();
#else
            Debug.LogError("[Quest] 任务配置还没加载，QuestManager 没启动就取配置了");
            return null;
#endif
        }
    }
}
