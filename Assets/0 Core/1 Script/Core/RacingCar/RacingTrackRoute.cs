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
    [Title("线路")]
    [LabelText("控制点(闭合)"), ListDrawerSettings(ShowFoldout = true)]
    [Tooltip("俯视 2D 坐标，单位米。首尾自动相连，不要重复填第一个点。用「赛道线路编辑器」窗口拖比手填舒服")]
    [SerializeField] List<Vector2> controlPoints = new List<Vector2>
    {
        new Vector2(-40f, 20f), new Vector2(40f, 20f), new Vector2(40f, -20f), new Vector2(-40f, -20f),
    };

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
    public List<Vector2> ControlPoints => controlPoints;

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

    /// <summary>里程 s 处的曲率（弧度/米），左转为正。这是喂给路面 shader 偏移表的量。</summary>
    public float CurvatureAt(float s)
    {
        if(!IsBaked)
            return 0f;

        SampleIndex(s, out int i0, out int i1, out float t);
        return Mathf.Lerp(bakedCurvature[i0], bakedCurvature[i1], t);
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
        float step = bakedLength / n;
        float u = Mathf.Repeat(s, bakedLength) / step;
        i0 = Mathf.Clamp((int)u, 0, n - 1);
        i1 = (i0 + 1) % n;
        t = u - i0;
    }
    #endregion

    #region 烘焙
    /// <summary>由控制点重算等弧长采样点、朝向与曲率。改控制点后必须调用。</summary>
    [Button("重新烘焙"), PropertyOrder(100)]
    public void Bake()
    {
        int cn = controlPoints?.Count ?? 0;
        if(cn < 3)
        {
            bakedPoints = null;
            bakedLength = 0f;
            return;
        }

        // 1. 闭合 Catmull-Rom 密采样。这一步的点是「参数等距」而非「弧长等距」，只用来量长度
        const int PerSegment = 24;
        int dn = cn * PerSegment;
        var dense = new Vector2[dn];
        for(int i = 0; i < cn; i++)
        {
            Vector2 p0 = controlPoints[(i - 1 + cn) % cn];
            Vector2 p1 = controlPoints[i];
            Vector2 p2 = controlPoints[(i + 1) % cn];
            Vector2 p3 = controlPoints[(i + 2) % cn];
            for(int j = 0; j < PerSegment; j++)
                dense[i * PerSegment + j] = CatmullRom(p0, p1, p2, p3, (float)j / PerSegment);
        }

        // 2. 累计弧长（闭合，最后一段回到起点）
        var cum = new float[dn + 1];
        for(int i = 0; i < dn; i++)
            cum[i + 1] = cum[i] + Vector2.Distance(dense[i], dense[(i + 1) % dn]);
        bakedLength = cum[dn];
        if(bakedLength <= 1e-4f)
        {
            bakedPoints = null;
            return;
        }

        // 3. 按等弧长重采样。之后 s 与数组下标就是线性关系，查表不用再二分
        int n = Mathf.Clamp(sampleCount, 64, 2048);
        bakedPoints = new Vector2[n];
        float step = bakedLength / n;
        int cursor = 0;
        for(int i = 0; i < n; i++)
        {
            float target = i * step;
            while(cursor < dn - 1 && cum[cursor + 1] < target)
                cursor++;
            float segLen = cum[cursor + 1] - cum[cursor];
            float t = segLen > 1e-6f ? (target - cum[cursor]) / segLen : 0f;
            bakedPoints[i] = Vector2.Lerp(dense[cursor], dense[(cursor + 1) % dn], t);
        }

        // 4. 朝向与曲率。曲率 = 相邻切线的有符号夹角 / 步长，左转为正
        bakedHeadings = new float[n];
        bakedCurvature = new float[n];
        for(int i = 0; i < n; i++)
        {
            Vector2 prev = bakedPoints[(i - 1 + n) % n];
            Vector2 cur = bakedPoints[i];
            Vector2 next = bakedPoints[(i + 1) % n];

            Vector2 t1 = (cur - prev).normalized;
            Vector2 t2 = (next - cur).normalized;
            bakedHeadings[i] = Mathf.Atan2(next.y - prev.y, next.x - prev.x);
            // 叉积定符号、点积定大小，比两次 Atan2 相减稳（不用处理 ±π 跨越）
            bakedCurvature[i] = Mathf.Atan2(t1.x * t2.y - t1.y * t2.x, Vector2.Dot(t1, t2)) / step;
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
                tmp[i] = (bakedCurvature[(i - 1 + n) % n] + bakedCurvature[i] * 2f + bakedCurvature[(i + 1) % n]) * 0.25f;
            System.Array.Copy(tmp, bakedCurvature, n);
        }
    }

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t
                       + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                       + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
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
