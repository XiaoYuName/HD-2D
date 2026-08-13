using UnityEngine;
using UnityEngine.AddressableAssets;

namespace XFramework
{
    /// <summary>运行时默认加载器：AssetReference 交给 AssetsManager 走 Addressables，复用它的缓存与引用计数。</summary>
    public class QuestConfigAaLoader : IQuestConfigLoader
    {
        readonly AssetReference reference;

        public QuestConfigAaLoader(AssetReference reference) => this.reference = reference;

        public QuestConfig Load()
        {
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                Debug.LogError("[Quest] 任务配置的 AssetReference 没配，检查 GameManager 上的 QuestManager");
                return null;
            }
            return AssetsManager.Instance.LoadAssets<QuestConfig>(reference);
        }
    }
}
