using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Flow;
using Drama.Runtime.Services;
using Spine.Unity;
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
    /// 只管"要加载的资源"（立绘 Spine 数据、背景图）。BGM 走配置表 ID，语音走多语言 Asset Table，
    /// 两者都不经这里。
    ///
    /// 生命周期：Director 每段剧本开始前预载、结束后调 <see cref="ReleaseAll"/>。
    /// </summary>
    public sealed class DramaAssetProvider : IDramaAssetProvider
    {
        /// <summary>
        /// 本段剧本已发起加载的 Key → 加载任务。
        ///
        /// 存任务而不是存结果，是为了让"同一个 Key 被预载两次"只真正加载一次：
        /// AssetsManager 按调用次数记引用（LoadAssetsUniTask 一次 +1，FreeAsset 一次 -1），
        /// 每个 Key 只许 Load 一次，<see cref="ReleaseAll"/> 才能一一对应地还干净。
        /// UniTask 默认只能 await 一次，所以入表前必须 Preserve()。
        /// </summary>
        private readonly Dictionary<string, object> loading = new Dictionary<string, object>();

        /// <summary>
        /// 立绘的 Spine 数据。
        ///
        /// <b>不在 <see cref="IDramaAssetProvider"/> 接口里</b>——包不该规定"一个立绘资源"是什么
        /// （Prefab？SkeletonDataAsset？贴图？各工程不一样），所以这是本工程自己的方法，
        /// 由 <c>DramaDirector.PreloadAsync</c> 和 <c>ActorController</c> 调。
        /// </summary>
        public UniTask<SkeletonDataAsset> LoadActorSkeletonAsync(int actorId, CancellationToken ct)
        {
            return LoadAsync<SkeletonDataAsset>(ResolveActorKey(actorId, EActorAssetKind.Spine), ct);
        }

        /// <summary>图片立绘的 Sprite。</summary>
        public UniTask<Sprite> LoadActorTextureAsync(int actorId, CancellationToken ct)
        {
            return LoadAsync<Sprite>(ResolveActorKey(actorId, EActorAssetKind.Texture), ct);
        }

        /// <summary>
        /// Live2D 立绘的模型预制体。
        ///
        /// 和另两种不一样：Live2D 是<b>一角色一预制体</b>（带 Animator 和 CubismRenderController），
        /// 不是"共用模板 + 换资源"。所以这里加载的是 Prefab 本身。
        /// </summary>
        public UniTask<GameObject> LoadActorCubismPrefabAsync(int actorId, CancellationToken ct)
        {
            return LoadAsync<GameObject>(ResolveActorKey(actorId, EActorAssetKind.Live2D), ct);
        }

        /// <summary>按类型预载一份立绘资源。<c>DramaDirector.PreloadAsync</c> 用。</summary>
        public UniTask PreloadActorAsync(ActorAssetRef actor, CancellationToken ct)
        {
            switch (actor.Kind)
            {
                case EActorAssetKind.Texture:
                    return LoadActorTextureAsync(actor.ActorId, ct);
                case EActorAssetKind.Live2D:
                    return LoadActorCubismPrefabAsync(actor.ActorId, ct);
                default:
                    return LoadActorSkeletonAsync(actor.ActorId, ct);
            }
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
        /// 按立绘类型去角色表取资源路径。<b>三种立绘的寻址只在这一处分叉</b>，
        /// 表结构变了也只改这里。
        ///
        /// 三个字段存的都是完整 AA 路径，剧本里的角色 ID 就是 NpcData 的 Id：
        ///     骨骼   → DramaSpinePaht，SkeletonDataAsset
        ///     图片   → TexturePath，一张图
        ///     Live2D → CusbimPath，<b>模型预制体</b>（带 Animator 和 CubismRenderController）
        /// </summary>
        private static string ResolveActorKey(int actorId, EActorAssetKind kind)
        {
            NpcData npc = LubanManager.Instance.TbNpcData.GetOrDefault(actorId);
            if (npc == null)
            {
                Debug.LogError($"[Drama] NpcData 里没有角色 {actorId}，跳过立绘加载");
                return null;
            }

            string path;
            switch (kind)
            {
                case EActorAssetKind.Texture: path = npc.TexturePath; break;
                case EActorAssetKind.Live2D:  path = npc.CusbimPath; break;
                default:                      path = npc.DramaSpinePaht; break;
            }

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[Drama] 角色 {actorId}（{npc.Remark}）没配{kind}立绘的资源路径");
                return null;
            }

            return path;
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
