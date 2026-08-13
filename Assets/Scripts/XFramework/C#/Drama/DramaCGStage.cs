using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime.Services;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// <see cref="IDramaCG"/> 的本工程实现：CG 层。
    ///
    /// <b>为什么不是 MonoBehaviour</b>：它在"还没有任何 CG"的时候就得存在（剧本可能一条 CG 都没有），
    /// 而 CG 模型是按需实例化的。所以这一层是个普通类，和
    /// <see cref="DramaAssetProvider"/> / <see cref="DramaAudio"/> 一样由 Director 持有。
    ///
    /// <b>单槽位</b>：同时只有一张 CG。换 CG 直接再调一次 <see cref="ShowAsync"/>，
    /// 本类负责把上一张收掉。
    /// </summary>
    public sealed class DramaCGStage : IDramaCG
    {
        /// <summary>资源层。由 Director 装配时塞进来（和它自己用的是同一个实例，才能命中预载的缓存）。</summary>
        public DramaAssetProvider Assets { get; set; }

        /// <summary>替身挂在哪。Canvas 里一个铺满全屏的 RectTransform。</summary>
        public RectTransform ProxyParent { get; set; }

        /// <summary>CG 模型实例挂在哪（世界空间）。</summary>
        public Transform ModelParent { get; set; }

        /// <summary>
        /// 立绘层的根节点。<b>进 CG 时整层藏掉，退出时恢复</b> ——
        /// 这是 CG 的固有语义，不靠剧本额外写隐藏指令来保证（漏配一条就穿帮）。
        /// </summary>
        public GameObject ActorLayer { get; set; }

        private CGController current;
        private long currentCgId = -1;

        /// <summary>正在跑的淡入淡出。<see cref="CompleteAllTweens"/> 要收得住它。</summary>
        private Tween fadeTween;

        public Transform Root => current != null ? current.Root : null;

        public async UniTask ShowAsync(long cgId, float duration, Ease ease, CancellationToken ct)
        {
            // 已经是这张了就只做淡入，别重新实例化 —— 重来一遍会把模型的 Animator 状态也清掉
            if (current != null && currentCgId == cgId)
            {
                await FadeAsync(1f, duration, ease, ct);
                return;
            }

            GameObject prefab = await LoadPrefabAsync(cgId, ct);
            if (prefab == null)
            {
                return;
            }

            // 换 CG：先把上一张收掉。这一步在加载之后做，加载失败时旧的还留着比闪一下空屏好
            DestroyCurrent();

            GameObject go = Object.Instantiate(prefab, ModelParent);
            go.SetActive(true);

            current = go.GetComponent<CGController>();
            if (current == null)
            {
                Debug.LogError($"[Drama] CG {cgId} 的预制体根节点上没有 CGController");
                Object.Destroy(go);
                return;
            }

            currentCgId = cgId;

            RectTransform proxy = CubismProxySync.CreateProxy($"CGProxy_{cgId}", ProxyParent);

            current.Init();
            current.Bind(cgId, proxy,
                         CubismProxySync.ResolveCanvasCamera(proxy),
                         CubismProxySync.ResolveCameraFor(go));

            // 立绘让位。放在这里而不是方法开头：加载失败时不该把立绘藏了
            SetActorLayerVisible(false);

            current.SetAlpha(0f);
            await FadeAsync(1f, duration, ease, ct);
        }

        public async UniTask HideAsync(float duration, Ease ease, CancellationToken ct)
        {
            if (current == null)
            {
                return;
            }

            await FadeAsync(0f, duration, ease, ct);

            DestroyCurrent();
            SetActorLayerVisible(true);
        }

        // ==================================================== Animator

        public void SetAnimatorBool(string parameterName, bool value) =>
            current?.SetAnimatorBool(parameterName, value);

        public void SetAnimatorInt(string parameterName, int value) =>
            current?.SetAnimatorInt(parameterName, value);

        public void SetAnimatorFloat(string parameterName, float value) =>
            current?.SetAnimatorFloat(parameterName, value);

        public void SetAnimatorTrigger(string parameterName, bool reset) =>
            current?.SetAnimatorTrigger(parameterName, reset);

        // ==================================================== 收尾

        /// <summary>
        /// 把还在跑的 CG 动画推到终点。
        ///
        /// 两类：本类自己建的淡入淡出（target 是闭包，DOTween.Complete(Root) 收不到），
        /// 以及 Handler 直接建在替身上的位移 / 缩放 / 抖动 / 无限循环小动作。
        /// </summary>
        public void CompleteAllTweens()
        {
            fadeTween?.Complete(true);
            fadeTween = null;

            if (Root != null)
            {
                DOTween.Complete(Root, withCallbacks: true);
            }
        }

        /// <summary>
        /// 清空 CG 层。剧本收尾时调 —— 剧本可能停在"CG 还盖着"的状态，
        /// 不清就是一张 CG 留在下一段剧情上。<b>立绘层也要恢复</b>，否则下一段剧情没有立绘。
        /// </summary>
        public void Clear()
        {
            CompleteAllTweens();
            DestroyCurrent();
            SetActorLayerVisible(true);
        }

        private void DestroyCurrent()
        {
            if (current == null)
            {
                currentCgId = -1;
                return;
            }

            // 替身在 Canvas 里、不在模型这棵树上，得让它自己先收
            current.DestroyProxy();
            Object.Destroy(current.gameObject);

            current = null;
            currentCgId = -1;
        }

        private UniTask FadeAsync(float target, float duration, Ease ease, CancellationToken ct)
        {
            if (current == null)
            {
                return UniTask.CompletedTask;
            }

            fadeTween?.Kill();
            fadeTween = null;

            if (duration <= 0f)
            {
                current.SetAlpha(target);
                return UniTask.CompletedTask;
            }

            CGController view = current;
            float from = target > 0.5f ? 0f : 1f;
            view.SetAlpha(from);

            fadeTween = DOTween.To(() => from, x =>
                                {
                                    from = x;
                                    if (view != null) view.SetAlpha(x);
                                }, target, duration)
                            .SetEase(ease);

            return fadeTween.ToUniTask(cancellationToken: ct);
        }

        private void SetActorLayerVisible(bool visible)
        {
            if (ActorLayer != null)
            {
                ActorLayer.SetActive(visible);
            }
        }

        private async UniTask<GameObject> LoadPrefabAsync(long cgId, CancellationToken ct)
        {
            if (Assets == null)
            {
                Debug.LogError("[Drama] DramaCGStage.Assets 没装配，CG 加载不了");
                return null;
            }

            GameObject prefab = await Assets.LoadCGPrefabAsync(cgId, ct);
            if (prefab == null)
            {
                Debug.LogError($"[Drama] 加载不到 CG {cgId} 的预制体");
            }

            return prefab;
        }
    }
}
