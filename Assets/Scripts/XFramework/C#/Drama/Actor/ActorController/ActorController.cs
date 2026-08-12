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

    /// <summary>图片立绘的模板，和骨骼那份同理。</summary>
    private UniTask<GameObject>? textureTemplateLoading;

    /// <summary>
    /// 在台上的立绘。ActorId → 实例。
    ///
    /// <b>一个角色同时只有一种立绘在台上</b>：剧本先用骨骼出场、后面又用图片出场同一个角色时，
    /// 后者会把前者换掉（见 <see cref="AcquireAsync"/>），否则 <see cref="Find"/>
    /// 拿到哪一个就成了随机的。
    /// </summary>
    private readonly Dictionary<int, IDramaActorView> onStage = new Dictionary<int, IDramaActorView>();

    /// <summary>台上每个角色用的是哪种立绘。换类型出场时要靠它判断是不是得先拆掉旧的。</summary>
    private readonly Dictionary<int, EActorAssetKind> onStageKinds = new Dictionary<int, EActorAssetKind>();

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
    public async UniTask<IActorView> AcquireAsync(int actorId, EActorAssetKind kind, CancellationToken ct)
    {
        // 同一个角色换一种立绘出场：先把旧的拆掉。不拆的话字典里只留得下一个，
        // 另一个会变成没人管的孤儿一直挂在屏幕上
        if (onStage.TryGetValue(actorId, out IDramaActorView exist))
        {
            if (onStageKinds.TryGetValue(actorId, out EActorAssetKind existKind) && existKind == kind)
            {
                return exist;
            }

            DestroyView(exist);
            onStage.Remove(actorId);
            onStageKinds.Remove(actorId);
        }

        if (Assets == null)
        {
            Debug.LogError("[Drama] ActorController.Assets 没装配，立绘加载不了");
            return null;
        }

        IDramaActorView view;
        switch (kind)
        {
            case EActorAssetKind.Texture: view = await CreateTextureViewAsync(actorId, ct); break;
            case EActorAssetKind.Live2D:  view = await CreateCubismViewAsync(actorId, ct); break;
            default:                      view = await CreateSkeletonViewAsync(actorId, ct); break;
        }

        if (view == null)
        {
            return null;   // 各分支自己报过错了
        }

        view.SetAlpha(0f);   // 入场前先透明，由 SetVisibleAsync 淡进来

        onStage.Add(actorId, view);
        onStageKinds.Add(actorId, kind);
        return view;
    }

    // ---- 三种立绘各自的创建

    private async UniTask<IDramaActorView> CreateSkeletonViewAsync(int actorId, CancellationToken ct)
    {
        // 模板和角色数据没有依赖关系，一起拉
        GameObject template = await LoadTemplateAsync(ref templateLoading, AssetKeys.ActorSkeletonControllerPath);
        SkeletonDataAsset skeletonData = await Assets.LoadActorSkeletonAsync(actorId, ct);

        if (template == null || skeletonData == null)
        {
            return null;
        }

        GameObject go = SpawnUnderCenter(template);

        ActorSkeletonController view = go.GetComponent<ActorSkeletonController>();
        if (view == null)
        {
            Debug.LogError($"[Drama] 立绘模板根节点上没有 ActorSkeletonController：{AssetKeys.ActorSkeletonControllerPath}");
            Destroy(go);
            return null;
        }

        view.Init();
        view.Bind(actorId, skeletonData);
        return view;
    }

    private async UniTask<IDramaActorView> CreateTextureViewAsync(int actorId, CancellationToken ct)
    {
        GameObject template = await LoadTemplateAsync(ref textureTemplateLoading, AssetKeys.ActorTextureControllerPath);
        Sprite sprite = await Assets.LoadActorTextureAsync(actorId, ct);

        if (template == null || sprite == null)
        {
            return null;
        }

        GameObject go = SpawnUnderCenter(template);

        ActorTextureController view = go.GetComponent<ActorTextureController>();
        if (view == null)
        {
            Debug.LogError($"[Drama] 图片立绘模板根节点上没有 ActorTextureController：{AssetKeys.ActorTextureControllerPath}");
            Destroy(go);
            return null;
        }

        view.Init();
        view.Bind(actorId, sprite);
        return view;
    }

    /// <summary>
    /// Live2D 立绘。和另两种最不一样的地方：模型在<b>世界空间</b>，
    /// Canvas 里只放一个替身，模型每帧跟着替身走（原因见 <see cref="ActorCubismController"/>）。
    ///
    /// Live2D 是<b>一角色一预制体</b>（角色表存的就是模型预制体路径），
    /// 不像另两种是"共用模板 + 换资源"，所以这里没有模板。
    /// </summary>
    private async UniTask<IDramaActorView> CreateCubismViewAsync(int actorId, CancellationToken ct)
    {
        GameObject prefab = await Assets.LoadActorCubismPrefabAsync(actorId, ct);
        if (prefab == null)
        {
            return null;
        }

        // 模型挂到 DramaManager 下（世界空间），不能进 Canvas
        Transform modelParent = DramaManager.IsInitialized ? DramaManager.Instance.transform : null;
        GameObject go = Instantiate(prefab, modelParent);
        go.SetActive(true);

        ActorCubismController view = go.GetComponent<ActorCubismController>();
        if (view == null)
        {
            Debug.LogError($"[Drama] Live2D 立绘预制体根节点上没有 ActorCubismController：角色 {actorId}");
            Destroy(go);
            return null;
        }

        // 替身：一个光秃秃的 RectTransform，挂在方向锚点下，按 UI 规则布局。
        // 运行时建而不是做成预制体 —— 它没有任何可配的东西
        RectTransform proxy = new GameObject($"Live2DProxy_{actorId}", typeof(RectTransform))
                              .GetComponent<RectTransform>();
        proxy.SetParent(directionCenter, false);
        proxy.localPosition = Vector3.zero;
        proxy.localScale = Vector3.one;
        proxy.sizeDelta = Vector2.zero;

        view.Init();
        view.Bind(actorId, proxy, ResolveCanvasCamera(proxy), ResolveCameraFor(go));
        return view;
    }

    /// <summary>替身所在 Canvas 的渲染相机。Overlay 模式下是 null，正是 WorldToScreenPoint 要的。</summary>
    private static Camera ResolveCanvasCamera(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    /// <summary>
    /// 找能拍到这个模型的相机：按它所在的 Layer 匹配 cullingMask，优先主相机。
    ///
    /// 不写死某台相机，是因为"Live2D 归谁拍"是场景搭建的事 ——
    /// 现在和背景共用主相机，以后单开一台 Cubism 相机时这里不用改。
    /// </summary>
    private static Camera ResolveCameraFor(GameObject model)
    {
        int mask = 1 << model.layer;

        if (Camera.main != null && (Camera.main.cullingMask & mask) != 0)
        {
            return Camera.main;
        }

        foreach (Camera cam in Camera.allCameras)
        {
            if ((cam.cullingMask & mask) != 0) return cam;
        }

        Debug.LogWarning($"[Drama] 没有相机拍得到 Live2D 所在的层「{LayerMask.LayerToName(model.layer)}」，" +
                         "立绘位置会不对。回退到主相机");
        return Camera.main;
    }

    /// <summary>默认摆中间，ActorShowAction 紧接着会按 Direction 覆盖位置。</summary>
    private GameObject SpawnUnderCenter(GameObject template)
    {
        GameObject go = Instantiate(template, directionCenter);
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;
        go.SetActive(true);      // 模板 Prefab 根节点可能是关着的
        return go;
    }

    /// <summary>加载立绘模板。每段剧本只真正加载一次，由 <see cref="ReleaseAll"/> 还引用。</summary>
    private static UniTask<GameObject> LoadTemplateAsync(ref UniTask<GameObject>? slot, string key)
    {
        if (slot == null)
        {
            // UniTask 默认只能 await 一次，缓存起来给后续角色复用必须 Preserve()
            slot = AssetsManager.Instance.LoadAssetsUniTask<GameObject>(key).Preserve();
        }

        return slot.Value;
    }

    /// <summary>找已经在台上的立绘；不在台上返回 null。</summary>
    public IActorView Find(int actorId)
    {
        return onStage.TryGetValue(actorId, out IDramaActorView view) ? view : null;
    }

    /// <summary>显隐。<paramref name="duration"/> 为 0 就是瞬间切换。</summary>
    public UniTask SetVisibleAsync(IActorView actor, bool visible, float duration, Ease ease, CancellationToken ct)
    {
        if (!(actor is IDramaActorView view))
        {
            return UniTask.CompletedTask;
        }

        float target = visible ? 1f : 0f;

        if (duration <= 0f)
        {
            view.SetAlpha(target);
            view.SetVisible(visible);
            return UniTask.CompletedTask;
        }

        // 淡入要先激活，不然看不到过程；淡出等跑完再关。
        // 走 SetVisible 而不是 Root.gameObject.SetActive —— Live2D 的 Root 是 Canvas 里的替身，
        // 关掉替身模型照样在屏幕上
        if (visible)
        {
            view.SetVisible(true);
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
            tween.OnComplete(() => view.SetVisible(false));
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
        //    退出 Play 时立绘实例可能已经被 Unity 销毁了，view != null 就是在挡这个
        foreach (IDramaActorView view in onStage.Values)
        {
            if (view != null && view.Root != null)
            {
                DOTween.Complete(view.Root, withCallbacks: true);
                view.CompleteHighlightTweens();   // 压暗/微缩的 target 不是 Root，上面那句收不到
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

        foreach (IDramaActorView view in onStage.Values)
        {
            DestroyView(view);
        }

        onStage.Clear();
        onStageKinds.Clear();

        // 模板是本类自己 Load 的（不经 Provider），所以也得自己还 ——
        // 一次 LoadAssetsUniTask 对一次 FreeAsset，多还少还都不行。
        // 先置 null 再还：退出 Play 时 AssetsManager 单例可能已经没了，
        // 那句抛出去也不至于让下次进来重复还一遍
        FreeTemplate(ref templateLoading, AssetKeys.ActorSkeletonControllerPath);
        FreeTemplate(ref textureTemplateLoading, AssetKeys.ActorTextureControllerPath);
    }

    private static void FreeTemplate(ref UniTask<GameObject>? slot, string key)
    {
        if (slot == null)
        {
            return;
        }

        slot = null;
        AssetsManager.Instance?.FreeAsset(key);
    }

    /// <summary>
    /// 拆掉一个立绘。先让它自己收（Live2D 要收 Canvas 里的替身），再销毁本体 ——
    /// 顺序反了替身就成了没人管的孤儿。
    /// </summary>
    private void DestroyView(IDramaActorView view)
    {
        if (view == null)
        {
            return;
        }

        view.ReleaseView();

        if (view is MonoBehaviour behaviour && behaviour != null)
        {
            Destroy(behaviour.gameObject);
        }
    }

    // ============================================================ 讲话人突出

    /// <summary>压暗 / 微缩的开关和强度，由 <c>ActorHighlightAction</c> 设置。</summary>
    private ActorHighlightSettings highlight = ActorHighlightSettings.Default;

    /// <summary>当前说话人，-1 = 没有具体说话人（旁白等）。</summary>
    private int speakerId = -1;

    public void SetHighlightMode(ActorHighlightSettings settings)
    {
        highlight = settings;

        // 开关变了立刻按当前说话人重刷一遍，不然要等下一句台词才生效 ——
        // 剧本中途关掉效果时，玩家会看到压暗状态一直挂着
        ApplyHighlight();
    }

    public void SetSpeaker(int actorId)
    {
        speakerId = actorId;
        ApplyHighlight();
    }

    /// <summary>
    /// 按当前说话人刷一遍全场：说话人恢复原样，其他人压暗 / 微缩。
    ///
    /// 对齐旧工程 <c>TCharItem.SetGray</c>：开关关掉时要<b>强制恢复原样</b>，
    /// 而不是"什么都不做"—— 不然关开关之前被压暗的立绘会一直暗着。
    /// </summary>
    private void ApplyHighlight()
    {
        foreach (KeyValuePair<int, IDramaActorView> pair in onStage)
        {
            if (pair.Value == null)
            {
                continue;
            }

            // speakerId <= 0（旁白）时没人是说话人，所有立绘都算"非说话人"吗？
            // 不是 —— 旧工程旁白时不会把全场压暗，所以这里当"全部恢复原样"处理
            bool dimmed = speakerId > 0 && pair.Key != speakerId;

            // 开关关掉 = 强度按 1 处理（原样），不是"跳过不管" ——
            // 跳过的话之前被压暗的立绘会一直暗着
            pair.Value.SetDim(dimmed && highlight.Dim ? highlight.DimBrightness : 1f);
            pair.Value.SetShrink(dimmed && highlight.Shrink ? highlight.ShrinkScale : 1f);
        }
    }

    /// <summary>
    /// 把立绘挂到对应方向的锚点下。
    ///
    /// <b>只改父节点，不写 localPosition</b> —— Handler 紧接着就会写 Position
    /// （那是相对方向的偏移），这里写了会立刻被覆盖。
    /// </summary>
    public void SetDirection(IActorView actor, EActorShowDirection direction)
    {
        if (actor?.Root == null)
        {
            return;
        }

        RectTransform anchor = GetDirectionAnchor(direction);
        if (anchor == null)
        {
            Debug.LogWarning($"[Drama] 方向 {direction} 对应的锚点没配，立绘位置可能不对");
            return;
        }

        // worldPositionStays: false —— 我们要的是"挂到锚点下、局部坐标归零"，
        // 保留世界坐标的话换锚点就等于没换
        actor.Root.SetParent(anchor, false);
        actor.Root.localPosition = Vector3.zero;
    }

    /// <summary>按剧本给的方向取入场锚点。</summary>
    private RectTransform GetDirectionAnchor(EActorShowDirection direction)
    {
        switch (direction)
        {
            case EActorShowDirection.Left:  return directionLeft;
            case EActorShowDirection.Right: return directionRight;
            default:                        return directionCenter;
        }
    }
}
