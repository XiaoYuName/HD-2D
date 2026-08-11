using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Services;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XFramework
{
    /// <summary>
    /// <see cref="IDramaAssetProvider"/> 的本工程实现：把剧本里的业务 ID 翻译成 AA Key，
    /// 再交给 <see cref="AssetsManager"/> 加载。
    ///
    /// 整个类真正跟本工程耦合的只有末尾那两个 ResolveXxxKey——
    /// 配置表结构变了就只改那两个方法，Drama 包和 Handler 层完全无感。
    ///
    /// 只管"要加载的资源"（立绘 Prefab、背景图）。BGM 走配置表 ID，语音走多语言 Asset Table，
    /// 两者都不经这里。
    ///
    /// 生命周期：Director 每段剧本开始前预载、结束后调 <see cref="ReleaseAll"/>。
    /// </summary>
    public sealed class DramaAssetProvider : IDramaAssetProvider
    {
        /// <summary>剧情立绘 Prefab 所在目录，见 <see cref="ResolveActorKey"/> 的说明。</summary>
        private const string ActorPrefabPath = "Assets/AddressableAssets/Remote/Prefabs/Character/DramaActor/";

        /// <summary>
        /// 本段剧本已发起加载的 Key → 加载任务。
        ///
        /// 存任务而不是存结果，是为了让"同一个 Key 被预载两次"只真正加载一次：
        /// AssetsManager 按调用次数记引用（LoadAssetsUniTask 一次 +1，FreeAsset 一次 -1），
        /// 每个 Key 只许 Load 一次，<see cref="ReleaseAll"/> 才能一一对应地还干净。
        /// UniTask 默认只能 await 一次，所以入表前必须 Preserve()。
        /// </summary>
        private readonly Dictionary<string, object> loading = new Dictionary<string, object>();

        public UniTask<GameObject> LoadActorAsync(int actorId, CancellationToken ct)
        {
            return LoadAsync<GameObject>(ResolveActorKey(actorId), ct);
        }

        public UniTask<Sprite> LoadBackgroundAsync(long backgroundId, CancellationToken ct)
        {
            return LoadAsync<Sprite>(ResolveBackgroundKey(backgroundId), ct);
        }

        // BGM 不经这里：MusicId 是音频配置表的 ID，clip 由 AudioConfiguration 自己持有，
        // 剧情既不加载也不释放。见 DramaAudio.PlayMusic。

        public void ReleaseAll()
        {
            foreach (string key in loading.Keys)
            {
                AssetsManager.Instance.FreeAsset(key);
            }

            loading.Clear();
        }

        /// <summary>
        /// 加载并登记引用。Key 为空（没配资源 / 查表失败）时返回 null 而不是抛异常——
        /// 缺一张立绘不该让整段剧情播不下去，Handler 那边会当没这个资源处理。
        /// </summary>
        private UniTask<T> LoadAsync<T>(string key, CancellationToken ct) where T : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                return UniTask.FromResult<T>(null);
            }

            if (!loading.TryGetValue(key, out object cached))
            {
                cached = AssetsManager.Instance.LoadAssetsUniTask<T>(key).Preserve();
                loading.Add(key, cached);
            }

            UniTask<T> task = (UniTask<T>)cached;

            // 取消只是让调用方不再等下去；底下那次加载照常跑完并留在表里，
            // 最后由 ReleaseAll 统一归还，不会漏引用。
            return ct.CanBeCanceled ? task.AttachExternalCancellation(ct) : task;
        }

        /// <summary>
        /// 立绘 Prefab 的 AA Key。剧本里的角色 ID 就是 NpcData 的 Id。
        ///
        /// ⚠️ NpcData 目前只有两个贴图名字段——SceneSpinePath（场景小人）和 MiniImg（旧 DramaUI 的半身像），
        /// 还没有"剧情立绘 Prefab"这一项，所以这里先拿 MiniImg 拼约定目录。
        /// 等表里补上字段（像 ItemData.IconName 那样直接存资源名），把这里换成读字段就行。
        /// </summary>
        private static string ResolveActorKey(int actorId)
        {
            NpcData npc = LubanManager.Instance.TbNpcData.GetOrDefault(actorId);
            if (npc == null)
            {
                Debug.LogError($"[Drama] NpcData 里没有角色 {actorId}，跳过立绘加载");
                return null;
            }

            if (string.IsNullOrEmpty(npc.MiniImg))
            {
                Debug.LogWarning($"[Drama] 角色 {actorId}（{npc.Remark}）没配立绘资源");
                return null;
            }

            return $"{ActorPrefabPath}{npc.MiniImg}.prefab";
        }

        /// <summary>背景图的 AA Key：剧本里的背景 ID 即 DramaBgData 的 ID，BgPath 就是完整 AA 路径。</summary>
        private static string ResolveBackgroundKey(long backgroundId)
        {
            DramaBgData bgData = LubanManager.Instance.TbDramaBgData .GetOrDefault(backgroundId);
            if (bgData == null)
            {
                Debug.LogError($"[Drama] DramaBgData 里没有场景 {backgroundId}，跳过背景切换");
                return null;
            }
            
            return bgData.BgPath;
        }
    }
}
