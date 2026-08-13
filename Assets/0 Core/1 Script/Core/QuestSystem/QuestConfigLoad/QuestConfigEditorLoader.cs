#if UNITY_EDITOR
using UnityEditor;

namespace XFramework
{
    /// <summary>
    /// 编辑器兜底加载器：没进游戏时（任务编辑器、引用目录、校验）按工程路径直接取资产，不启动 AA。
    /// </summary>
    public class QuestConfigEditorLoader : IQuestConfigLoader
    {
        public QuestConfig Load() => AssetDatabase.LoadAssetAtPath<QuestConfig>(QuestConfig.AssetPath);
    }
}
#endif
