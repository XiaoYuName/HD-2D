using UnityEngine;

/// <summary>
/// 「Canvas 里的替身 → 世界空间的 Cubism 模型」的换算。Live2D 立绘和 Live2D CG 共用。
///
/// <b>为什么需要替身</b>：Cubism 模型是每个 Drawable 一个 MeshRenderer，没有
/// Graphic/CanvasRenderer 实现（Spine 能待在 Canvas 里是因为官方专门做了 SkeletonGraphic），
/// 所以它只能待在世界空间、走相机分层。但剧本指令是按 UI 口径写的（锚点 + 像素偏移），
/// 世界空间没有锚点 —— 于是在 Canvas 里放一个空的 RectTransform 当替身，
/// 剧本指令动替身，模型每帧照替身的位姿换算过来。
///
/// 这样分辨率一变，替身作为真正的 UI 元素会被 CanvasScaler 和锚点重新摆好，模型自动跟上，
/// 不需要在代码里复刻一遍 CanvasScaler 的逻辑（Match 混合模式手工复现很难对）。
/// </summary>
public sealed class CubismProxySync
{
    /// <summary>Canvas 里的替身。剧本指令动的是它。</summary>
    public RectTransform Proxy { get; private set; }

    /// <summary>替身所在 Canvas 的渲染相机（Overlay 模式下是 null，正是 WorldToScreenPoint 要的）。</summary>
    private Camera uiCamera;

    /// <summary>拍模型那一层的相机。</summary>
    private Camera modelCamera;

    /// <summary>模型放在相机前多远。正交相机下只影响 z，不影响 x/y。</summary>
    private float planeDistance = 10f;

    /// <summary>
    /// UI 单位 → 世界单位的换算系数，<see cref="Bind"/> 时按预制体里摆好的缩放自动反推。
    ///
    /// <b>为什么需要它</b>：<see cref="Apply"/> 每帧会把模型的 localScale 整个覆盖掉，
    /// 所以美术在预制体里摆的大小运行时不算数，得有个系数把"模型该多大"带回来。
    ///
    /// <b>为什么能自动推</b>：入场那一刻按"预制体里摆好的 localScale"反解一次即可 ——
    /// 于是美术把模型缩放到想要的大小，剧本里「缩放 1」就正好是那个大小。
    /// 之后分辨率变化照样跟得上，因为每帧重算的是 UI→世界的比例，
    /// 这个系数只负责模型自身的基准尺寸。
    /// </summary>
    private float calibratedScale = 1f;

    private bool calibrationFailed;

    /// <summary>入场时调一次。<paramref name="model"/> 的 localScale 会被当成基准尺寸取走。</summary>
    public void Bind(Transform model, RectTransform proxy, Camera canvasCamera, Camera cameraForModel)
    {
        Proxy = proxy;
        uiCamera = canvasCamera;
        modelCamera = cameraForModel;

        if (modelCamera != null)
        {
            planeDistance = Mathf.Abs(model.position.z - modelCamera.transform.position.z);
            if (planeDistance <= 0.01f) planeDistance = 10f;
        }

        Calibrate(model.localScale.x);
        Apply(model, 1f);
    }

    /// <summary>
    /// 把替身的位姿换算到模型上。
    ///
    /// <b>调用方必须放在 LateUpdate</b>：DOTween 默认在 Update 跑、Canvas 布局在那之后才重建，
    /// 放 Update 里会慢一帧 —— 抖动这类高频指令能明显看出来。
    /// </summary>
    /// <param name="extraScale">额外倍率（讲话人微缩之类），和剧本的缩放相乘。</param>
    public void Apply(Transform model, float extraScale)
    {
        if (Proxy == null || modelCamera == null || model == null)
        {
            return;
        }

        // ① 位置：替身的世界坐标 → 屏幕 → 模型相机的世界坐标
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, Proxy.position);
        model.position = modelCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, planeDistance));

        // ② 缩放 = UI→世界的比例 × 模型基准尺寸 × 剧本的缩放 × 额外倍率。
        //    比例每帧现量，分辨率 / CanvasScaler 一变它就跟着变
        model.localScale = Vector3.one * MeasureUiToWorld() * calibratedScale
                           * Proxy.localScale.x * extraScale;

        // ③ 旋转直接抄
        model.rotation = Proxy.rotation;
    }

    private void Calibrate(float authoredScale)
    {
        float uiToWorld = MeasureUiToWorld();

        if (uiToWorld <= 0.000001f || authoredScale <= 0f)
        {
            calibratedScale = 1f;
            calibrationFailed = true;
            return;
        }

        calibratedScale = authoredScale / uiToWorld;
    }

    /// <summary>标定失败（相机没找着 / 预制体缩放是 0）。调用方自己决定怎么报。</summary>
    public bool CalibrationFailed => calibrationFailed;

    /// <summary>
    /// 1 个 UI 单位等于多少世界单位。
    ///
    /// 现场量而不是算：取屏幕上相距 100 像素的两点，看它们在模型相机里隔多远。
    /// 分辨率 / CanvasScaler 一变这个比例就变，所以不能缓存。
    /// </summary>
    private float MeasureUiToWorld()
    {
        if (modelCamera == null || Proxy == null)
        {
            return 0f;
        }

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, Proxy.position);
        Vector2 probe = screen + new Vector2(100f, 0f);

        float world = (modelCamera.ScreenToWorldPoint(new Vector3(probe.x, probe.y, planeDistance))
                       - modelCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, planeDistance))).x;

        return world / 100f;
    }

    // ==================================================== 共用的查找逻辑

    /// <summary>替身所在 Canvas 的渲染相机。Overlay 模式下返回 null，正是 WorldToScreenPoint 要的。</summary>
    public static Camera ResolveCanvasCamera(RectTransform rect)
    {
        Canvas canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
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
    public static Camera ResolveCameraFor(GameObject model)
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

        Debug.LogWarning($"[Drama] 没有相机拍得到「{LayerMask.LayerToName(model.layer)}」层，" +
                         $"{model.name} 的位置会不对。回退到主相机");
        return Camera.main;
    }

    /// <summary>
    /// 建一个替身。运行时建而不是做成预制体 —— 它没有任何可配的东西。
    /// </summary>
    public static RectTransform CreateProxy(string name, RectTransform parent)
    {
        RectTransform proxy = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        proxy.SetParent(parent, false);
        proxy.localPosition = Vector3.zero;
        proxy.localScale = Vector3.one;
        proxy.sizeDelta = Vector2.zero;
        return proxy;
    }
}
