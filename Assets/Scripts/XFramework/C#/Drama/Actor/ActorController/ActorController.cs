using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime;
using Drama.Runtime.Services;
using Spine.Unity;
using UnityEngine;
using XFramework;

/// <summary>
/// 立绘舞台。<see cref="IActorStage"/> 的实现：管 ActorId → <see cref="ActorSkeletonController"/>
/// 的实例、入场位置、显隐动画和释放。
///
/// <b>加载走 <see cref="IDramaAssetProvider"/> 而不是自己调 AssetsManager</b>：
/// Director 开播前已经按 DramaAssetKeys 把本段用到的立绘全预载过了，
/// 走同一个 Provider 才能命中它的缓存，也才不会把引用计数记成两次。
/// </summary>
public partial class ActorController : UIBase, IActorStage
{
    /// <summary>
    /// 立绘资源从哪来。由 <c>DramaManager.StartDramaRuntime</c> 装配时塞进来
    /// （就是 Director 自己那个 Provider 实例）。
    ///
    /// 类型是具体的 <see cref="DramaAssetProvider"/> 而不是 <see cref="IDramaAssetProvider"/>：
    /// 加载 Spine 数据的方法是本工程特有的，不在包的接口上。
    /// </summary>
    public DramaAssetProvider Assets { get; set; }

    /// <summary>
    /// 立绘模板的加载任务。<b>所有角色共用这一个 Prefab</b>，实例化之后靠换
    /// SkeletonDataAsset 变成具体角色（角色表里 illustPath 存的是 SkeletonData 资产路径）。
    ///
    /// 走 AA 加载而不是 <c>[SerializeField]</c> 拖引用：拖引用会让 DramaRuntimeUI 这个
    /// 预制体把整条 Spine 依赖链拽进自己的 bundle，UI 一打开就得连模板一起加载。
    ///
    /// 存任务而不是存结果，是为了让同一段剧本里连续入场几个角色只真正加载一次
    /// （和 DramaAssetProvider 里那个缓存同一个道理）。
    /// </summary>
    private UniTask<GameObject>? templateLoading;

    /// <summary>在台上的立绘。ActorId → 实例。</summary>
    private readonly Dictionary<int, ActorSkeletonController> onStage = new Dictionary<int, ActorSkeletonController>();

    /// <summary>
    /// 舞台自己发起的显隐动画。
    ///
    /// 必须记下来：<see cref="ActorShowAction.WaitForCompletion"/> 为 false 时 Handler
    /// 立刻返回、动画还在跑，得靠 <see cref="CompleteAllTweens"/> 收口，
    /// 否则会漏到下一段剧情里去。
    /// </summary>
    private readonly List<Tween> stageTweens = new List<Tween>();

    public override void Init()
    {
        InitAutoBind();
    }

    /// <summary>拿到（必要时加载并入场）指定角色的立绘。</summary>
    public async UniTask<IActorView> AcquireAsync(int actorId, CancellationToken ct)
    {
        if (onStage.TryGetValue(actorId, out ActorSkeletonController exist))
        {
            return exist;
        }

        if (Assets == null)
        {
            Debug.LogError("[Drama] ActorController.Assets 没装配，立绘加载不了");
            return null;
        }

        // 模板和角色数据没有依赖关系，一起拉
        GameObject template = await LoadTemplateAsync();
        SkeletonDataAsset skeletonData = await Assets.LoadActorSkeletonAsync(actorId, ct);

        if (template == null || skeletonData == null)
        {
            return null;   // 各自那边已经报过错了
        }

        // 默认摆中间，ActorShowAction 紧接着会按 Direction 覆盖位置
        GameObject go = Instantiate(template, directionCenter);
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;
        go.SetActive(true);      // 模板 Prefab 根节点可能是关着的

        ActorSkeletonController view = go.GetComponent<ActorSkeletonController>();
        if (view == null)
        {
            Debug.LogError($"[Drama] 立绘模板根节点上没有 ActorSkeletonController：{AssetKeys.ActorSkeletonControllerPath}");
            Destroy(go);
            return null;
        }

        view.Init();
        view.Bind(actorId, skeletonData);
        view.SetAlpha(0f);                    // 入场前先透明，由 SetVisibleAsync 淡进来

        onStage.Add(actorId, view);
        return view;
    }

    /// <summary>加载立绘模板。每段剧本只真正加载一次，由 <see cref="ReleaseAll"/> 还引用。</summary>
    private UniTask<GameObject> LoadTemplateAsync()
    {
        if (templateLoading == null)
        {
            // UniTask 默认只能 await 一次，缓存起来给后续角色复用必须 Preserve()
            templateLoading = AssetsManager.Instance
                                           .LoadAssetsUniTask<GameObject>(AssetKeys.ActorSkeletonControllerPath)
                                           .Preserve();
        }

        return templateLoading.Value;
    }

    /// <summary>找已经在台上的立绘；不在台上返回 null。</summary>
    public IActorView Find(int actorId)
    {
        return onStage.TryGetValue(actorId, out ActorSkeletonController view) ? view : null;
    }

    /// <summary>显隐。<paramref name="duration"/> 为 0 就是瞬间切换。</summary>
    public UniTask SetVisibleAsync(IActorView actor, bool visible, float duration, Ease ease, CancellationToken ct)
    {
        if (!(actor is ActorSkeletonController view))
        {
            return UniTask.CompletedTask;
        }

        float target = visible ? 1f : 0f;

        if (duration <= 0f)
        {
            view.SetAlpha(target);
            view.Root.gameObject.SetActive(visible);
            return UniTask.CompletedTask;
        }

        // 淡入要先激活，不然看不到过程；淡出等跑完再关
        if (visible)
        {
            view.Root.gameObject.SetActive(true);
        }

        float from = visible ? 0f : 1f;
        view.SetAlpha(from);

        Tween tween = DOTween.To(() => from, x =>
                          {
                              from = x;
                              view.SetAlpha(x);
                          }, target, duration)
                      .SetEase(ease);

        if (!visible)
        {
            tween.OnComplete(() => view.Root.gameObject.SetActive(false));
        }

        // ★ 登记到舞台名下，CompleteAllTweens 才收得住
        stageTweens.Add(tween);
        tween.OnKill(() => stageTweens.Remove(tween));

        return tween.ToUniTask(cancellationToken: ct);
    }

    /// <summary>
    /// 把所有还在跑的立绘动画立刻推到终点。
    /// 剧本结束 / 跳转 / 切到 Skip 时调，防止游离动画漏到下一段剧情里。
    /// </summary>
    public void CompleteAllTweens()
    {
        // ① Handler 直接建在 Root 上的那些（位移 / 缩放 / 旋转 / 小动作）
        foreach (ActorSkeletonController view in onStage.Values)
        {
            if (view != null)
            {
                DOTween.Complete(view.Root, withCallbacks: true);
            }
        }

        // ② 舞台自己建的显隐动画（它们的 target 是闭包不是 Transform，上面那句收不到）
        //    倒着走：Complete 会触发 OnKill → 从 stageTweens 里移除自己
        for (int i = stageTweens.Count - 1; i >= 0; i--)
        {
            stageTweens[i]?.Complete(true);
        }

        stageTweens.Clear();
    }

    /// <summary>
    /// 清空舞台。
    ///
    /// 只销毁实例，<b>不释放 Prefab 的资源引用</b> —— 那是 Provider.ReleaseAll 的事，
    /// Director 会在本方法之后调它（顺序不能反，反了就是拿已释放的资源去 Destroy 实例）。
    /// </summary>
    public void ReleaseAll()
    {
        CompleteAllTweens();

        foreach (ActorSkeletonController view in onStage.Values)
        {
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        onStage.Clear();

        // 模板是本类自己 Load 的（不经 Provider），所以也得自己还 ——
        // 一次 LoadAssetsUniTask 对一次 FreeAsset，多还少还都不行
        if (templateLoading != null)
        {
            templateLoading = null;
            AssetsManager.Instance.FreeAsset(AssetKeys.ActorSkeletonControllerPath);
        }
    }

    /// <summary>按剧本给的方向取入场锚点。<see cref="ActorShowAction"/> 摆位置时用得上。</summary>
    public RectTransform GetDirectionAnchor(EActorShowDirection direction)
    {
        switch (direction)
        {
            case EActorShowDirection.Left:  return directionLeft;
            case EActorShowDirection.Right: return directionRight;
            default:                        return directionCenter;
        }
    }
}
