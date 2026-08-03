using System;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 赛车小游戏的赛道装配入口：读 Luban 的 <c>TbRacingTrackData</c> 行，把线路资产同时装配给
/// 3D 路面（<see cref="UIRasterScroll"/>）和小地图（<see cref="RacingTrackMinimapUI"/>）。
///
/// 数据分两层是有意的：
///   数值（圈数/车速/视距/限时/解锁）在 Luban 表里，策划照常改 Excel；
///   形状（中心线控制点）在 <see cref="RacingTrackRoute"/> 资产里，用「赛道线路编辑器」拖，表里只存它的 Addressable 路径。
/// 形状塞不进 Excel——一条赛道几十个二维点，手填不现实，而编辑器每次拖点回写 xlsx 又要起 Excel COM，太慢。
/// 这跟 SprayPaintGameData 用 ClothPrefabPath 引用预制体是同一个套路。
///
/// 圈数进度直接由里程算：路面和小地图共用同一个 Travel，所以三者天然一致，不存在各算各的。
/// </summary>
[AddComponentMenu("MiniGame/Racing Track Controller (赛道装配)")]
public class RacingTrackController : MonoBehaviour
{
    [Title("节点")]
    [LabelText("路面驱动器"), Required]
    [SerializeField] UIRasterScroll road;

    [LabelText("小地图")]
    [SerializeField] RacingTrackMinimapUI minimap;

    [Title("赛道")]
    [LabelText("赛道ID"), MinValue(1)]
    [SerializeField] long trackId = 1;

    [LabelText("启动时自动装配")]
    [SerializeField] bool loadOnStart = true;

    RacingTrackData _config;
    RacingTrackRoute _route;

    /// <summary>当前赛道的 Luban 配置行，未装配时为 null。</summary>
    public RacingTrackData Config => _config;

    /// <summary>当前赛道线路。</summary>
    public RacingTrackRoute Route => _route;

    /// <summary>已完成圈数（含小数）。</summary>
    public float LapProgress =>
        road != null && _route != null && _route.TotalLength > 0f ? road.Travel / _route.TotalLength : 0f;

    /// <summary>是否已跑完配置的总圈数。</summary>
    public bool IsFinished => _config != null && LapProgress >= _config.LapCount;

    /// <summary>跑完全程时触发一次。</summary>
    public event Action OnFinished;

    bool _finishedFired;

    void Start()
    {
        if(loadOnStart)
            Load(trackId);
    }

    void Update()
    {
        if(_finishedFired || _config == null || !IsFinished)
            return;

        _finishedFired = true;
        OnFinished?.Invoke();
    }

    /// <summary>装配指定赛道。表里查不到该 ID 或线路资产加载失败时会报错并保持原样。</summary>
    [Button("装配赛道"), PropertyOrder(100)]
    public void Load(long id)
    {
        RacingTrackData row = LubanManager.Instance.TbRacingTrackData?.GetOrDefault(id);
        if(row == null)
        {
            Debug.LogError($"赛道配置缺失: TbRacingTrackData 没有 ID={id} 的行");
            return;
        }

        var route = AssetsManager.Instance.LoadAssets<RacingTrackRoute>(row.RoutePath);
        if(route == null)
        {
            Debug.LogError($"赛道线路加载失败: {row.RoutePath} (赛道 ID={id})");
            return;
        }

        trackId = id;
        _config = row;
        _route = route;
        _finishedFired = false;

        if(road != null)
            road.ApplyTrack(route, row.ViewDistance, row.FullDeflectRadius, row.CarSpeed);

        if(minimap != null)
            minimap.SetRoute(route);
    }
}
