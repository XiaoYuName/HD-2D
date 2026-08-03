using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 赛车小游戏的赛道线路资产：一条俯视视角的 2D 闭合中心线。
///
/// 这是赛道的唯一真相。小地图和 3D 路面都从它派生，不各存一份：
///   小地图 = 中心线本身；
///   3D 路面 = 中心线求导出的曲率表（<see cref="CurvatureAt"/>），喂给 UIRasterScroll 的逐扫描线偏移。
///
/// 反过来（策划配曲率、积分出形状）行不通：闭合要同时满足 ∮κ ds = 2πn 与 ∮e^(iθ) ds = 0，
/// 改一个弯全赛道都得跟着调，参考图里 CIRCUIT No.3 那种赛道根本画不出来。
/// 由形状求曲率则只是一次差分，闭合天然成立。
///
/// 控制点走闭合 Catmull-Rom 插值，烘焙时按「等弧长」重采样，
/// 所以 <see cref="TotalLength"/> 之内的任意 s 都能直接查位置/朝向/曲率，
/// 车辆进度、圈数、小地图光点、路面弯道全部由这同一个 s 驱动，天然同步。
/// </summary>
[CreateAssetMenu(fileName = "RacingTrackRoute", menuName = ConfigMenuNameSet.MiniGame + "RacingTrackRoute")]
public class RacingTrackRoute : ScriptableObject
{
    /// <summary>
    /// 一个控制点。切线是相对本点的偏移量，和 Unity 曲线编辑器里的手柄一个意思。
    ///
    /// <see cref="Auto"/> 为真时切线不存、由相邻点现算：out = (下一点 - 上一点) / 6。
    /// 这个系数不是随手取的——它正好让三次贝塞尔和均匀 Catmull-Rom 完全等价，
    /// 所以老资产（只有点、没有切线）升上来形状一像素不变。
    /// </summary>
    [System.Serializable]
    public class TrackPoint
    {
        [LabelText("坐标")] public Vector2 Position;
        [LabelText("入切线")] public Vector2 InTangent;
        [LabelText("出切线")] public Vector2 OutTangent;
        [LabelText("自动切线")] public bool Auto = true;
        [LabelText("断开两侧")] public bool Broken;

        public TrackPoint() { }
        public TrackPoint(Vector2 p) { Position = p; }
    }

    [Title("线路")]
    [LabelText("控制点(闭合)"), ListDrawerSettings(ShowFoldout = true)]
    [Tooltip("俯视 2D 坐标，单位米。首尾自动相连，不要重复填第一个点。用「赛道线路编辑器」窗口拖比手填舒服")]
    [SerializeField] List<TrackPoint> trackPoints = new List<TrackPoint>
    {
        new TrackPoint(new Vector2(-40f, 20f)), new TrackPoint(new Vector2(40f, 20f)),
        new TrackPoint(new Vector2(40f, -20f)), new TrackPoint(new Vector2(-40f, -20f)),
    };

    // 旧版只存了裸坐标。保留字段用于一次性迁移，迁完就清空
    [SerializeField, HideInInspector] List<Vector2> controlPoints;

    // 迁移标记。不能用「trackPoints 是否为空」来判断——它有字段初始化器，
    // 反序列化老资产时初始化器先把 4 个默认点填进去，判空永远不成立，老坐标就被丢了
    [SerializeField, HideInInspector] int dataVersion;

    [LabelText("首尾相连")]
    [Tooltip("勾上 = 环形赛道，起点即终点，可以跑多圈。\n" +
             "取消 = 开放赛道，最后一个点是终点，跑到就结束（圈数按 1 圈处理）")]
    [SerializeField] bool closed = true;

    [Title("曲率")]
    [LabelText("最紧弯道半径(米)"), MinValue(1f)]
    [Tooltip("曲率的硬上限：比这更急的弯按这个半径算。手拖控制点很容易在两点之间拖出折角，" +
             "那里算出来的曲率能到几十倍于正常弯道——路面会直接推到满偏移、离心力会超过针头的最大移速，" +
             "那一段玩家再怎么打方向也救不回来，必然 0 分。\n" +
             "夹住之后急弯变成「等半径的紧弯」，还是难，但打得回来。\n" +
             "参考：12m 对应路面中线在针尖那一行外移约 185px，而针头行程是 ±420px")]
    [SerializeField] float minCornerRadius = 12f;

    [Title("烘焙")]
    [LabelText("采样数"), PropertyRange(64, 2048)]
    [Tooltip("等弧长重采样的点数。决定曲率表精度，256~512 足够")]
    [SerializeField] int sampleCount = 384;

    [LabelText("曲率平滑次数"), PropertyRange(0, 16)]
    [Tooltip("手拖的控制点会让曲率有毛刺，路面上表现为方向盘抖。做几次三点平滑压掉")]
    [SerializeField] int curvatureSmoothing = 4;

    // ↓ 烘焙产物。序列化进资产，运行时直接查表，不重算
    [SerializeField, HideInInspector] Vector2[] bakedPoints;
    [SerializeField, HideInInspector] float[] bakedHeadings;   // 弧度，+X 为 0，逆时针为正
    [SerializeField, HideInInspector] float[] bakedCurvature;  // 弧度/米，左转为正
    [SerializeField, HideInInspector] float bakedLength;
    [SerializeField, HideInInspector] Rect bakedBounds;

    /// <summary>赛道一圈总长（米）。</summary>
    public float TotalLength => bakedLength;

    /// <summary>等弧长重采样后的中心线点列，闭合（最后一点与第一点相邻，不重复）。</summary>
    public IReadOnlyList<Vector2> Points => bakedPoints;

    /// <summary>中心线的包围盒，小地图归一化用。</summary>
    public Rect Bounds => bakedBounds;

    /// <summary>是否已烘焙可用。</summary>
    public bool IsBaked => bakedPoints != null && bakedPoints.Length >= 4 && bakedLength > 0f;

    /// <summary>控制点列表，编辑器工具用。改完记得 <see cref="Bake"/>。</summary>
    public List<TrackPoint> ControlPoints
    {
        get
        {
            MigrateLegacy();
            return trackPoints;
        }
    }

    /// <summary>老资产里只有裸坐标，升级成带切线的控制点（切线设为自动，形状不变）。只跑一次。</summary>
    void MigrateLegacy()
    {
        if(dataVersion >= 1)
            return;

        dataVersion = 1;

        // 新建的资产没有老数据，保留字段初始化器给的默认形状
        if(controlPoints == null || controlPoints.Count == 0)
            return;

        // 整表替换而不是「空了才填」：字段初始化器已经塞了默认点，判空是不成立的
        trackPoints = new List<TrackPoint>(controlPoints.Count);
        foreach(Vector2 p in controlPoints)
            trackPoints.Add(new TrackPoint(p));

        controlPoints.Clear();
    }

    /// <summary>
    /// 解析第 i 个点的切线（自动模式下现算）。
    /// out = (下一点 - 上一点) / 6，in = -out —— 这正是均匀 Catmull-Rom 的贝塞尔等价形式。
    /// </summary>
    public void ResolveTangents(int i, out Vector2 inT, out Vector2 outT)
    {
        int n = trackPoints.Count;
        TrackPoint p = trackPoints[i];

        if(!p.Auto)
        {
            inT = p.InTangent;
            outT = p.OutTangent;
            return;
        }

        Vector2 auto;
        if(closed)
        {
            auto = (trackPoints[(i + 1) % n].Position - trackPoints[(i - 1 + n) % n].Position) / 6f;
        }
        else if(i == 0)
        {
            // 开放曲线的首点没有「上一点」，退化成单边差分；系数 1/3 才能和内部点的 1/6 双边差分接上
            auto = (trackPoints[1].Position - trackPoints[0].Position) / 3f;
        }
        else if(i == n - 1)
        {
            auto = (trackPoints[n - 1].Position - trackPoints[n - 2].Position) / 3f;
        }
        else
        {
            auto = (trackPoints[i + 1].Position - trackPoints[i - 1].Position) / 6f;
        }

        inT = -auto;
        outT = auto;
    }

    /// <summary>是否环形赛道。开放赛道的最后一个点是终点，不与起点相连。改完要 <see cref="Bake"/>。</summary>
    public bool IsClosed
    {
        get => closed;
        set => closed = value;
    }

    /// <summary>闭合曲线至少 3 个点才成环，开放曲线 2 个点就能画一条线。</summary>
    public int MinPointCount => closed ? 3 : 2;

    /// <summary>曲线段数：环形 = 点数，开放 = 点数 - 1。</summary>
    public int SegmentCount => trackPoints == null ? 0 : Mathf.Max(0, closed ? trackPoints.Count : trackPoints.Count - 1);

    void OnEnable()
    {
        if(!IsBaked)
            Bake();
    }

    #region 采样
    /// <summary>里程 s（米）处的中心线位置。s 自动按一圈长度回绕，可以直接喂负数或超过一圈的值。</summary>
    public Vector2 PositionAt(float s)
    {
        if(!IsBaked)
            return Vector2.zero;

        SampleIndex(s, out int i0, out int i1, out float t);
        return Vector2.Lerp(bakedPoints[i0], bakedPoints[i1], t);
    }

    /// <summary>里程 s 处的行进朝向（弧度）。按方向向量插值，不会在 ±π 处跳变。</summary>
    public float HeadingAt(float s)
    {
        if(!IsBaked)
            return 0f;

        SampleIndex(s, out int i0, out int i1, out float t);
        Vector2 d = Vector2.Lerp(
            new Vector2(Mathf.Cos(bakedHeadings[i0]), Mathf.Sin(bakedHeadings[i0])),
            new Vector2(Mathf.Cos(bakedHeadings[i1]), Mathf.Sin(bakedHeadings[i1])), t);
        return Mathf.Atan2(d.y, d.x);
    }

    /// <summary>
    /// 里程 s 处的曲率（弧度/米），左转为正。这是喂给路面 shader 偏移表和离心力的量。
    ///
    /// 出口按 <see cref="minCornerRadius"/> 夹住，而不是直接返回烘焙值：曲率是「手拖的形状求两次导数」，
    /// 相邻控制点之间只要有折角，这里就能算出几十倍于正常弯道的值——路面推满偏移把中线甩到针头够不到的地方、
    /// 离心力超过针头的最大移速，那一段无论怎么操作都是 0 分。夹在读取端而不是烘焙端，
    /// 是为了改半径立刻见效、且不必重烘所有旧资产。
    ///
    /// 代价：小地图画的是没夹过的中心线（<see cref="Points"/>），折角处两边会有肉眼几乎看不出的形状差。
    /// 真要完全一致，就把线拖顺——曲率不该靠夹来救。
    /// </summary>
    public float CurvatureAt(float s)
    {
        if(!IsBaked)
            return 0f;

        // 开放赛道在终点之外返回 0：路面每帧要往前看 120m，快到终点时会读到越界的里程，
        // 夹到端点曲率的话路会一直弯下去，返回 0 才是「路到头了，前方笔直」
        if(!closed && (s < 0f || s > bakedLength))
            return 0f;

        SampleIndex(s, out int i0, out int i1, out float t);
        float k = Mathf.Lerp(bakedCurvature[i0], bakedCurvature[i1], t);

        float max = 1f / Mathf.Max(minCornerRadius, 1f);
        return Mathf.Clamp(k, -max, max);
    }

    /// <summary>曲率上限对应的弯道半径（米）。比这更急的弯会被 <see cref="CurvatureAt"/> 夹平。</summary>
    public float MinCornerRadius
    {
        get => minCornerRadius;
        set => minCornerRadius = Mathf.Max(value, 1f);
    }

    /// <summary>
    /// 烘焙数据里最急的那个弯有多急（米）。用来体检手拖的线：
    /// 这个值远小于 <see cref="minCornerRadius"/> 就说明线上有折角，正被夹平，该回去把控制点拖顺。
    /// </summary>
    [ShowInInspector, ReadOnly, PropertyOrder(1)]
    [LabelText("实测最急弯道半径(米)")]
    public float BakedTightestRadius
    {
        get
        {
            if(bakedCurvature == null || bakedCurvature.Length == 0)
                return 0f;

            float peak = 0f;
            foreach(float k in bakedCurvature)
                peak = Mathf.Max(peak, Mathf.Abs(k));

            return peak > 1e-5f ? 1f / peak : float.PositiveInfinity;
        }
    }

    /// <summary>里程 s 处的位置，归一化到包围盒的 0~1（小地图摆光点用）。</summary>
    public Vector2 NormalizedAt(float s)
    {
        Vector2 p = PositionAt(s);
        Rect b = bakedBounds;
        return new Vector2(
            b.width > 0f ? (p.x - b.xMin) / b.width : 0.5f,
            b.height > 0f ? (p.y - b.yMin) / b.height : 0.5f);
    }

    void SampleIndex(float s, out int i0, out int i1, out float t)
    {
        int n = bakedPoints.Length;

        if(closed)
        {
            // 环形：n 个采样点对应 n 段（最后一段绕回起点），里程按一圈回绕
            float stepC = bakedLength / n;
            float uC = Mathf.Repeat(s, bakedLength) / stepC;
            i0 = Mathf.Clamp((int)uC, 0, n - 1);
            i1 = (i0 + 1) % n;
            t = uC - i0;
            return;
        }

        // 开放：n 个采样点只有 n-1 段，最后一点正好落在里程末端；越界夹住而不是回绕
        float step = bakedLength / (n - 1);
        float u = Mathf.Clamp(s, 0f, bakedLength) / step;
        i0 = Mathf.Clamp((int)u, 0, n - 2);
        i1 = i0 + 1;
        t = u - i0;
    }
    #endregion

    #region 烘焙
    /// <summary>由控制点重算等弧长采样点、朝向与曲率。改控制点后必须调用。</summary>
    [Button("重新烘焙"), PropertyOrder(100)]
    public void Bake()
    {
        MigrateLegacy();

        int cn = trackPoints?.Count ?? 0;
        if(cn < MinPointCount)
        {
            bakedPoints = null;
            bakedLength = 0f;
            return;
        }

        // 1. 三次贝塞尔密采样。这一步的点是「参数等距」而非「弧长等距」，只用来量长度。
        //    环形有 cn 段（最后一段绕回起点），开放只有 cn-1 段，且要把终点本身补进去
        const int PerSegment = 24;
        int segs = closed ? cn : cn - 1;
        int dn = segs * PerSegment + (closed ? 0 : 1);
        var dense = new Vector2[dn];
        for(int i = 0; i < segs; i++)
        {
            int next = (i + 1) % cn;
            ResolveTangents(i, out _, out Vector2 outT);
            ResolveTangents(next, out Vector2 inT, out _);

            Vector2 b0 = trackPoints[i].Position;
            Vector2 b1 = b0 + outT;
            Vector2 b3 = trackPoints[next].Position;
            Vector2 b2 = b3 + inT;

            for(int j = 0; j < PerSegment; j++)
                dense[i * PerSegment + j] = Bezier(b0, b1, b2, b3, (float)j / PerSegment);
        }
        if(!closed)
            dense[dn - 1] = trackPoints[cn - 1].Position;   // 终点

        // 2. 累计弧长。环形要把「最后一点回到起点」那一段也算进去，开放到终点为止
        int links = closed ? dn : dn - 1;
        var cum = new float[links + 1];
        for(int i = 0; i < links; i++)
            cum[i + 1] = cum[i] + Vector2.Distance(dense[i], dense[(i + 1) % dn]);
        bakedLength = cum[links];
        if(bakedLength <= 1e-4f)
        {
            bakedPoints = null;
            return;
        }

        // 3. 按等弧长重采样。之后 s 与数组下标就是线性关系，查表不用再二分。
        //    环形 n 个点对应 n 段；开放 n 个点只有 n-1 段，最后一点必须正好落在终点上
        int n = Mathf.Clamp(sampleCount, 64, 2048);
        bakedPoints = new Vector2[n];
        float step = closed ? bakedLength / n : bakedLength / (n - 1);
        int cursor = 0;
        for(int i = 0; i < n; i++)
        {
            float target = i * step;
            while(cursor < links - 1 && cum[cursor + 1] < target)
                cursor++;
            float segLen = cum[cursor + 1] - cum[cursor];
            float t = segLen > 1e-6f ? (target - cum[cursor]) / segLen : 0f;
            bakedPoints[i] = Vector2.Lerp(dense[cursor], dense[(cursor + 1) % dn], t);
        }

        // 4. 朝向与曲率。曲率 = 相邻切线的有符号夹角 / 步长，左转为正。
        //    开放曲线的两端没有邻居，取最近的内部值，别让端点算出假曲率
        bakedHeadings = new float[n];
        bakedCurvature = new float[n];
        for(int i = 0; i < n; i++)
        {
            if(!closed && (i == 0 || i == n - 1))
                continue;

            Vector2 prev = bakedPoints[(i - 1 + n) % n];
            Vector2 cur = bakedPoints[i];
            Vector2 next = bakedPoints[(i + 1) % n];

            Vector2 t1 = (cur - prev).normalized;
            Vector2 t2 = (next - cur).normalized;
            bakedHeadings[i] = Mathf.Atan2(next.y - prev.y, next.x - prev.x);
            // 叉积定符号、点积定大小，比两次 Atan2 相减稳（不用处理 ±π 跨越）
            bakedCurvature[i] = Mathf.Atan2(t1.x * t2.y - t1.y * t2.x, Vector2.Dot(t1, t2)) / step;
        }

        if(!closed && n >= 3)
        {
            bakedHeadings[0] = Mathf.Atan2(bakedPoints[1].y - bakedPoints[0].y, bakedPoints[1].x - bakedPoints[0].x);
            bakedHeadings[n - 1] = Mathf.Atan2(bakedPoints[n - 1].y - bakedPoints[n - 2].y, bakedPoints[n - 1].x - bakedPoints[n - 2].x);
            bakedCurvature[0] = bakedCurvature[1];
            bakedCurvature[n - 1] = bakedCurvature[n - 2];
        }

        SmoothCurvature(curvatureSmoothing);

        // 5. 包围盒
        Vector2 min = bakedPoints[0], max = bakedPoints[0];
        for(int i = 1; i < n; i++)
        {
            min = Vector2.Min(min, bakedPoints[i]);
            max = Vector2.Max(max, bakedPoints[i]);
        }
        bakedBounds = new Rect(min, max - min);
    }

    void SmoothCurvature(int iterations)
    {
        int n = bakedCurvature.Length;
        var tmp = new float[n];
        for(int it = 0; it < iterations; it++)
        {
            for(int i = 0; i < n; i++)
            {
                // 开放曲线的两端不能绕回另一头去平滑，否则终点的曲率会被起点污染
                int prev = closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0);
                int next = closed ? (i + 1) % n : Mathf.Min(i + 1, n - 1);
                tmp[i] = (bakedCurvature[prev] + bakedCurvature[i] * 2f + bakedCurvature[next]) * 0.25f;
            }
            System.Array.Copy(tmp, bakedCurvature, n);
        }
    }

    /// <summary>三次贝塞尔求值。b1/b2 是两端的切线手柄位置（不是相对偏移）。</summary>
    public static Vector2 Bezier(Vector2 b0, Vector2 b1, Vector2 b2, Vector2 b3, float t)
    {
        float u = 1f - t;
        return u * u * u * b0
             + 3f * u * u * t * b1
             + 3f * u * t * t * b2
             + t * t * t * b3;
    }
    #endregion

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if(this != null)
                Bake();
        };
    }
#endif
}
