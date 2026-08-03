using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 赛道小地图。把 <see cref="RacingTrackRoute"/> 烘成贴图铺在 RawImage 上，并按里程摆车辆光点。
///
/// 和 3D 路面的同步靠的是同一个里程 s：把 <see cref="road"/> 指向驱动路面的 UIRasterScroll，
/// 本组件每帧读它的 Travel，于是「小地图上车头前面那个弯」和「画面里正在扑过来的弯」是同一个弯，
/// 不存在两套数据对不上的问题。
///
/// 层级要求：<see cref="carMarker"/> 必须是 <see cref="mapImage"/> 的子节点且锚点居中，
/// 本组件用 anchoredPosition 摆它。
/// </summary>
[AddComponentMenu("UI/Racing Track Minimap (赛道小地图)")]
[ExecuteAlways]
public class RacingTrackMinimapUI : MonoBehaviour
{
    [Title("数据")]
    [LabelText("赛道线路")]
    [SerializeField] RacingTrackRoute route;

    [LabelText("路面驱动器")]
    [Tooltip("指向驱动 3D 路面的 UIRasterScroll，小地图会跟着它的里程走。留空则由外部写 Progress")]
    [SerializeField] UIRasterScroll road;

    [Title("节点")]
    [LabelText("地图图片"), Required]
    [SerializeField] RawImage mapImage;

    [LabelText("车辆光点")]
    [Tooltip("mapImage 的子节点，锚点请设为居中")]
    [SerializeField] RectTransform carMarker;

    [LabelText("光点跟随朝向")]
    [SerializeField] bool rotateCarMarker = true;

    [Title("外观")]
    [LabelText("样式"), InlineProperty, HideLabel]
    [SerializeField] RacingTrackMinimapStyle style = new RacingTrackMinimapStyle();

    Texture2D _texture;
    RacingTrackRoute _builtFor;

    /// <summary>当前里程（米）。<see cref="road"/> 有值时每帧被覆盖。</summary>
    public float Progress { get; set; }

    /// <summary>当前圈内进度 0~1。</summary>
    public float LapProgress01 =>
        route != null && route.TotalLength > 0f ? Mathf.Repeat(Progress, route.TotalLength) / route.TotalLength : 0f;

    void OnEnable() => Rebuild();

    void OnDisable() => ReleaseTexture();

    void LateUpdate()
    {
        if(route == null || !route.IsBaked)
            return;

        if(_builtFor != route)
            Rebuild();

        if(road != null)
            Progress = road.Travel;

        PlaceMarker();
    }

    /// <summary>
    /// 按 Luban 的赛道配置行装配小地图：加载 RoutePath 指向的线路资产并重烘贴图。
    /// 和路面消费的是同一个 RoutePath，AssetsManager 有缓存，不会重复加载。
    /// 释放由调用方（面板）在关闭时统一 FreeAsset，本组件不持有所有权。
    /// </summary>
    public void SetData(RacingTrackData data)
    {
        if(data == null)
        {
            Debug.LogError($"{name}: 赛道配置为空，小地图未装配");
            return;
        }

        var value = AssetsManager.Instance.LoadAssets<RacingTrackRoute>(data.RoutePath);
        if(value == null)
        {
            Debug.LogError($"{name}: 赛道线路加载失败 RoutePath={data.RoutePath} (赛道 ID={data.ID})");
            return;
        }

        SetRoute(value);
    }

    /// <summary>换赛道。会重烘小地图贴图。</summary>
    public void SetRoute(RacingTrackRoute value)
    {
        route = value;
        Rebuild();
    }

    /// <summary>重烘小地图贴图。改样式或线路后调用。</summary>
    [Button("重烘小地图"), PropertyOrder(100)]
    public void Rebuild()
    {
        ReleaseTexture();

        if(route == null || !route.IsBaked)
            return;

        _texture = RacingTrackMinimap.Bake(route, style);
        _builtFor = route;

        if(mapImage != null)
            mapImage.texture = _texture;

        PlaceMarker();
    }

    void PlaceMarker()
    {
        if(carMarker == null || mapImage == null || route == null || !route.IsBaked)
            return;

        Vector2 uv = RacingTrackMinimap.WorldToUV(route, style, route.PositionAt(Progress));
        Rect r = mapImage.rectTransform.rect;
        // uv 是贴图内归一化坐标，减 0.5 换成「相对图片中心」的偏移，锚点居中时可直接当 anchoredPosition
        carMarker.anchoredPosition = new Vector2((uv.x - 0.5f) * r.width, (uv.y - 0.5f) * r.height);

        if(rotateCarMarker)
            carMarker.localRotation = Quaternion.Euler(0f, 0f, route.HeadingAt(Progress) * Mathf.Rad2Deg - 90f);
    }

    void ReleaseTexture()
    {
        if(_texture == null)
            return;

        if(mapImage != null && mapImage.texture == _texture)
            mapImage.texture = null;

        if(Application.isPlaying)
            Destroy(_texture);
        else
            DestroyImmediate(_texture);

        _texture = null;
        _builtFor = null;
    }
}
