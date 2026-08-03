using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 缝纫拖尾：针头扎过的地方留下一串黑色虚线，模拟缝纫机缝出来的线迹。
///
/// 关键在于线迹是「缝在布上」的，不是贴在屏幕上的——车往前开，已经缝好的针脚要跟着路面
/// 一起朝观众涌过来、逐渐变粗、最后从画面下方溜出去。所以针脚存的是**路面坐标**：
///   s       —— 缝下这一针时的赛道里程（米）
///   lateral —— 当时针尖偏离路面中心多远，换算成「距离 1 米处的像素数」存
/// 每帧再用 UIRasterScroll 的透视映射把 (s, lateral) 投回屏幕，于是透视、弯道、滚动速度
/// 全都和路面严格一致，不需要单独对齐。
///
/// 线迹存活时间很短：矩形底边对应车前方约 metersPerDepth 米，针尖大概在十几米处，
/// 所以一针从缝下到溜出画面只有零点几秒。这是对的——参考画面里针头后面也就跟着两三节线。
///
/// 层级要求：本节点必须和路面 RawImage **同矩形**（直接做它的子节点并拉伸铺满最省事），
/// 否则 v 和横向偏移的换算会错位。
/// </summary>
[AddComponentMenu("MiniGame/Racing Stitch Trail (缝纫拖尾)")]
public class RacingStitchTrail : MaskableGraphic
{
    struct Stitch
    {
        public float S;            // 缝下时的赛道里程（米）
        public float LateralUnit;  // 横向偏移 × 当时的距离，投影时除以当前距离即可
    }

    [Title("数据来源")]
    [LabelText("路面驱动器"), Required]
    [SerializeField] UIRasterScroll road;

    [LabelText("针尖节点"), Required]
    [Tooltip("针真正扎下去的那个点。会读它在本矩形内的位置，所以放在针尖上，别放针杆中间")]
    [SerializeField] RectTransform needleTip;

    [Title("线迹")]
    [LabelText("对齐白色中线")]
    [Tooltip("开启后针距/实线占比/线宽全部从路面材质的中线参数反推，并把落点吸附到白线的里程网格上。\n" +
             "于是走直线且压着中线时，黑线迹与白虚线逐段完全重合。关掉才用下面三个手填值")]
    [SerializeField] bool matchCenterLine = true;

    [LabelText("针距(米)"), MinValue(0.05f), HideIf(nameof(matchCenterLine))]
    [SerializeField] float stitchSpacing = 1.2f;

    [LabelText("实线占比"), PropertyRange(0.05f, 1f), HideIf(nameof(matchCenterLine))]
    [SerializeField] float dashRatio = 0.55f;

    [LabelText("线宽(像素)"), MinValue(0.5f), HideIf(nameof(matchCenterLine))]
    [Tooltip("针尖那一行的宽度。更近的地方会按透视自动变粗")]
    [SerializeField] float lineWidth = 12f;

    [LabelText("最多保留针数"), PropertyRange(16, 512)]
    [SerializeField] int maxStitches = 96;

    readonly List<Stitch> _stitches = new List<Stitch>();
    float _lastStitchS = float.NegativeInfinity;
    float _tipDistance = 1f;

    /// <summary>当前留存的针数。</summary>
    public int StitchCount => _stitches.Count;

    protected override void Awake()
    {
        base.Awake();
        // 拖尾自己不采样贴图，纯色顶点即可
        if(material == null)
            material = defaultMaterial;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Clear();
    }

    void LateUpdate()
    {
        // 放在 LateUpdate：此时路面已经推进过 Travel、针头也已经移动完，读到的是本帧最终位置
        if(road == null || needleTip == null)
            return;

        RecordStitch();
        DropExpired();
        SetVerticesDirty();
    }

    /// <summary>清空线迹。重开一局 / 换赛道时调。</summary>
    [Button("清空线迹"), PropertyOrder(100)]
    public void Clear()
    {
        _stitches.Clear();
        _lastStitchS = float.NegativeInfinity;
        SetVerticesDirty();
    }

    #region 采样
    /// <summary>一针的里程周期（实线 + 空档）。对齐模式下等于白色中线的周期。</summary>
    float Period => matchCenterLine && road.CenterLinePeriod > 1e-4f ? road.CenterLinePeriod : stitchSpacing;

    /// <summary>实线占周期的比例。</summary>
    float Ratio => matchCenterLine ? road.CenterLineRatio : dashRatio;

    void RecordStitch()
    {
        if(!TryReadTip(out float tipX, out float tipV))
            return;

        _tipDistance = Mathf.Max(road.GetDistanceAt(tipV), 0.01f);
        float s = road.Travel + _tipDistance;
        float period = Mathf.Max(Period, 1e-3f);

        // 吸附到白线的里程网格：白线的实线正好落在 [k*period, (k+ratio)*period)，
        // 所以只在跨过整数格时下针、并把落点对齐到格子起点，两条线才会逐段重合。
        // 不吸附的话相位会差一个随机偏移，看起来就是「黑线插在白线中间」
        float grid = Mathf.Floor(s / period) * period;
        if(grid - _lastStitchS < period * 0.5f)
            return;

        // 里程发生跳变（换赛道 Travel 归零、首帧布局还没算好导致 v 取到离谱值等）时只重新对齐，
        // 不补针——否则会凭空生出一段跨越整个画面的线迹
        if(grid - _lastStitchS > period * 4f && _stitches.Count > 0)
        {
            _lastStitchS = grid;
            return;
        }

        _lastStitchS = grid;

        Rect r = rectTransform.rect;
        float centerX = r.center.x + road.GetRoadCenterOffset01(tipV) * r.width;

        // 存「偏移 × 距离」：投影时除以当前距离就自动完成透视缩放，不用记录参考行
        _stitches.Add(new Stitch
        {
            S = grid,
            LateralUnit = (tipX - centerX) * _tipDistance,
        });

        if(_stitches.Count > maxStitches)
            _stitches.RemoveRange(0, _stitches.Count - maxStitches);
    }

    /// <summary>读针尖在本矩形内的位置：x 是本地坐标，v 是 0(底)~1(顶) 的归一化行。</summary>
    bool TryReadTip(out float x, out float v)
    {
        x = 0f;
        v = 0f;

        Vector3 world = needleTip.position;
        Vector2 local = rectTransform.InverseTransformPoint(world);

        Rect r = rectTransform.rect;
        if(r.height <= 0f)
            return false;

        x = local.x;
        v = Mathf.Clamp01((local.y - r.yMin) / r.height);
        return true;
    }

    void DropExpired()
    {
        // 已经从画面下方溜出去的针不再需要，从头部裁掉（列表天然按里程升序）
        int drop = 0;
        for(int i = 0; i < _stitches.Count; i++)
        {
            if(road.TryGetRowV(_stitches[i].S - road.Travel, out _))
                break;
            drop++;
        }

        if(drop > 0)
            _stitches.RemoveRange(0, drop);

        // 尾部：针不可能缝到针尖前面去。真出现了说明记录时里程或布局是脏的，直接丢掉，
        // 否则这一针会一直挂在针尖前方画出一段永远追不上的线
        float limit = _tipDistance + Mathf.Max(Period, 1e-3f);
        int cut = _stitches.Count;
        while(cut > 0 && _stitches[cut - 1].S - road.Travel > limit)
            cut--;

        if(cut < _stitches.Count)
            _stitches.RemoveRange(cut, _stitches.Count - cut);
    }
    #endregion

    #region 绘制
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if(road == null || _stitches.Count < 2)
            return;

        float dashLen = Period * Ratio;

        // 每一针都要画，包括最新的那一针——写成 Count-1 的话最新一针要等下一针记录才出现，
        // 针尖底下会缺一段，正好露出下面的白色中线
        for(int i = 0; i < _stitches.Count; i++)
        {
            Stitch a = _stitches[i];

            // 一针 = 一段实线，从 a 起、长度 dashLen，剩下的是空档
            float endS = a.S + dashLen;
            float endLateral = a.LateralUnit;

            // 有下一针时按里程插值出末端的横向位置，转向时线迹才跟着斜过去
            if(i + 1 < _stitches.Count)
            {
                Stitch b = _stitches[i + 1];
                float t = Mathf.Approximately(b.S, a.S) ? 0f : Mathf.Clamp01((endS - a.S) / (b.S - a.S));
                endLateral = Mathf.Lerp(a.LateralUnit, b.LateralUnit, t);
            }

            if(!TryProject(a.S, a.LateralUnit, out Vector2 p0, out float w0))
                continue;
            if(!TryProject(endS, endLateral, out Vector2 p1, out float w1))
                continue;

            AddQuad(vh, p0, p1, w0, w1);
        }
    }

    /// <summary>把路面坐标 (里程, 横向) 投到本矩形的本地坐标，同时给出该处的线宽。</summary>
    bool TryProject(float s, float lateralUnit, out Vector2 local, out float width)
    {
        local = default;
        width = 0f;

        float distance = s - road.Travel;
        if(!road.TryGetRowV(distance, out float v))
            return false;

        Rect r = rectTransform.rect;
        float centerX = r.center.x + road.GetRoadCenterOffset01(v) * r.width;

        local = new Vector2(centerX + lateralUnit / distance, r.yMin + v * r.height);

        // 对齐模式下线宽直接取白中线在该距离的宽度（shader 里 halfW = _LineWidth * nearSoft，
        // 而 nearSoft = metersPerDepth / distance），两条线粗细逐行相同
        width = matchCenterLine
            ? road.CenterLineWidth01(distance) * r.width
            : lineWidth * _tipDistance / distance;
        return true;
    }

    void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, float w0, float w1)
    {
        Vector2 dir = p1 - p0;
        if(dir.sqrMagnitude < 1e-6f)
            return;

        // 法线取线段自身的垂线，拐弯时线迹会跟着倾斜，比恒定水平的横杠自然
        Vector2 n = new Vector2(-dir.y, dir.x).normalized;
        Vector2 n0 = n * (w0 * 0.5f);
        Vector2 n1 = n * (w1 * 0.5f);

        int idx = vh.currentVertCount;
        Color32 c = color;
        vh.AddVert(p0 - n0, c, Vector2.zero);
        vh.AddVert(p0 + n0, c, Vector2.zero);
        vh.AddVert(p1 + n1, c, Vector2.zero);
        vh.AddVert(p1 - n1, c, Vector2.zero);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }
    #endregion
}
