using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 程序化「圆角方形（可虚线）描边」UI，用顶点网格直接绘制圆角矩形边框，
/// 任意缩放都清晰，不依赖任何 Sprite 或九宫格图片。
///
/// 起因：破线（点线）的圆角边框位图是周期图案，无法用 <see cref="NineSliceShrinker"/>
/// 收成九宫格，也无法靠拉伸缩放；改由本组件按 RectTransform 尺寸实时生成即可自适应。
///
/// 特性：实线 / 虚线（dash + gap，任一为 0 即实线）可沿闭合周长均匀对齐；
/// 圆角半径、线宽、圆角平滑度全部参数化；可选内部填充；描边颜色取 Graphic 自带 Color。
///
/// 用法：挂到一个 UI 节点（像 Image 那样），不需要 Sprite；线宽向内绘制，描边外缘对齐 RectTransform 边界。
/// </summary>
[AddComponentMenu("UI/Dashed Rounded Rect (程序化圆角虚线框)")]
public class DashedRoundedRect : MaskableGraphic
{
    [Title("描边")]
    [LabelText("线宽(px)"), MinValue(0f)]
    [SerializeField] float thickness = 4f;

    [LabelText("圆角半径(px)"), MinValue(0f)]
    [SerializeField] float cornerRadius = 24f;

    [LabelText("每个圆角分段数"), Range(1, 64), Tooltip("越大圆角越圆滑，顶点也越多")]
    [SerializeField] int cornerSegments = 8;

    [Title("虚线 (实线段或间隔任一为 0 即实线)")]
    [LabelText("实线段长度(px)"), MinValue(0f)]
    [SerializeField] float dashLength = 16f;

    [LabelText("间隔长度(px)"), MinValue(0f)]
    [SerializeField] float dashGap = 12f;

    [LabelText("沿周长均匀对齐"), Tooltip("自动微调周期，使虚线在闭合边框首尾无缝衔接，不留半截")]
    [SerializeField] bool evenDashes = true;

    [Title("填充")]
    [LabelText("填充内部")]
    [SerializeField] bool fill = false;

    [LabelText("填充颜色"), ShowIf(nameof(fill))]
    [SerializeField] Color fillColor = new(1f, 1f, 1f, 0.15f);

    const float Eps = 1e-4f;

    // 中线路径缓存，跨实例复用避免每帧 GC。
    static readonly List<Vector2> pathPosList = new();
    static readonly List<Vector2> pathNrmList = new();
    static readonly List<float> pathCumList = new(); // 到达各顶点的累积弧长
    static readonly List<float> pathSegList = new(); // 各顶点到下一点(闭合)的段长
    static float pathLen;                            // 闭合周长

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float w = rect.width, h = rect.height;
        if (w <= 0f || h <= 0f) return;

        bool drawStroke = thickness > 0f && color.a > 0f;
        bool drawFill = fill && fillColor.a > 0f;
        if (!drawStroke && !drawFill) return;

        // 线宽向内绘制，描边外缘对齐 rect，故中线内缩 half。
        float half = Mathf.Min(thickness * 0.5f, Mathf.Min(w, h) * 0.5f);
        float left = rect.xMin + half, right = rect.xMax - half;
        float bottom = rect.yMin + half, top = rect.yMax - half;
        float maxR = Mathf.Min(right - left, top - bottom) * 0.5f;
        float cr = Mathf.Clamp(cornerRadius - half, 0f, Mathf.Max(0f, maxR));

        CreatePath(left, right, bottom, top, cr);

        if (drawFill)
            AddFill(vh, rect.center, half);

        if (!drawStroke) return;

        if (dashLength <= 0f || dashGap <= 0f)
        {
            AddDash(vh, 0f, pathLen, half); // 实线：整条闭合边框
            return;
        }

        float dash = dashLength;
        float period = dashLength + dashGap;
        if (evenDashes)
        {
            int cycleCount = Mathf.Max(1, Mathf.RoundToInt(pathLen / period));
            period = pathLen / cycleCount;                        // 缩放到整数个周期，闭合处无缝
            dash = period * (dashLength / (dashLength + dashGap)); // 保持 dash 与 gap 的比例
        }

        for (float from = 0f; from < pathLen - Eps; from += period)
            AddDash(vh, from, Mathf.Min(from + dash, pathLen), half);
    }

    // 生成闭合圆角矩形中线（4 段圆弧，直边为弧与弧之间的连接段）并算好弧长表。
    void CreatePath(float left, float right, float bottom, float top, float cr)
    {
        pathPosList.Clear();
        pathNrmList.Clear();

        // CCW 顺序：右下 到 右上 到 左上 到 左下，弧之间的连接段即为四条直边。
        AddArc(new Vector2(right - cr, bottom + cr), cr, 270f, 360f);
        AddArc(new Vector2(right - cr, top - cr), cr, 0f, 90f);
        AddArc(new Vector2(left + cr, top - cr), cr, 90f, 180f);
        AddArc(new Vector2(left + cr, bottom + cr), cr, 180f, 270f);

        pathCumList.Clear();
        pathSegList.Clear();
        int count = pathPosList.Count;
        pathLen = 0f;
        for (int i = 0; i < count; i++)
        {
            pathCumList.Add(pathLen);
            float segLen = (pathPosList[(i + 1) % count] - pathPosList[i]).magnitude;
            pathSegList.Add(segLen);
            pathLen += segLen;
        }
    }

    void AddArc(Vector2 centerPos, float radius, float degFrom, float degTo)
    {
        for (int i = 0; i <= cornerSegments; i++)
        {
            float rad = Mathf.Lerp(degFrom, degTo, (float)i / cornerSegments) * Mathf.Deg2Rad;
            Vector2 nrm = new(Mathf.Cos(rad), Mathf.Sin(rad)); // 外法线
            pathPosList.Add(centerPos + nrm * radius);
            pathNrmList.Add(nrm);
        }
    }

    // 按弧长 dist 在闭合中线上采样出位置与外法线。
    void GetPathPoint(float dist, out Vector2 pos, out Vector2 nrm)
    {
        dist = Mathf.Clamp(dist, 0f, pathLen);
        int count = pathPosList.Count;
        for (int i = 0; i < count; i++)
        {
            float segLen = pathSegList[i];
            if (i == count - 1 || dist <= pathCumList[i] + segLen)
            {
                float t = segLen > Eps ? (dist - pathCumList[i]) / segLen : 0f;
                int j = (i + 1) % count;
                pos = Vector2.LerpUnclamped(pathPosList[i], pathPosList[j], t);
                nrm = Vector2.LerpUnclamped(pathNrmList[i], pathNrmList[j], t).normalized;
                return;
            }
        }
        pos = pathPosList[0];
        nrm = pathNrmList[0];
    }

    // 把中线区间 [from, to] 生成一条带宽度的三角形带（虚线的一段，直接取自圆角形状）。
    void AddDash(VertexHelper vh, float from, float to, float half)
    {
        int count = pathPosList.Count;
        int baseIdx = vh.currentVertCount;
        Color32 col = color;

        GetPathPoint(from, out Vector2 startPos, out Vector2 startNrm);
        AddVert(vh, startPos + startNrm * half, col);
        AddVert(vh, startPos - startNrm * half, col);

        // 落在区间内的原顶点也要保留，才能还原圆角弧形。
        for (int i = 0; i < count; i++)
        {
            float cum = pathCumList[i];
            if (cum <= from + Eps || cum >= to - Eps) continue;
            AddVert(vh, pathPosList[i] + pathNrmList[i] * half, col);
            AddVert(vh, pathPosList[i] - pathNrmList[i] * half, col);
        }

        GetPathPoint(to, out Vector2 endPos, out Vector2 endNrm);
        AddVert(vh, endPos + endNrm * half, col);
        AddVert(vh, endPos - endNrm * half, col);

        int quadCount = (vh.currentVertCount - baseIdx) / 2 - 1;
        for (int i = 0; i < quadCount; i++)
        {
            int outer0 = baseIdx + i * 2, inner0 = outer0 + 1;
            int outer1 = outer0 + 2, inner1 = outer0 + 3;
            vh.AddTriangle(outer0, inner0, outer1);
            vh.AddTriangle(inner0, inner1, outer1);
        }
    }

    // 以内边界(中线内缩 half)为轮廓，从中心扇形填充内部。
    void AddFill(VertexHelper vh, Vector2 centerPos, float half)
    {
        int count = pathPosList.Count;
        Color32 col = fillColor;

        int centerIdx = vh.currentVertCount;
        AddVert(vh, centerPos, col);
        for (int i = 0; i < count; i++)
            AddVert(vh, pathPosList[i] - pathNrmList[i] * half, col);

        for (int i = 0; i < count; i++)
            vh.AddTriangle(centerIdx, centerIdx + 1 + i, centerIdx + 1 + (i + 1) % count);
    }

    static void AddVert(VertexHelper vh, Vector2 pos, Color32 col)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = pos;
        v.color = col;
        v.uv0 = Vector2.zero; // 无贴图，取白图 (0,0) 恒为白
        vh.AddVert(v);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        cornerSegments = Mathf.Clamp(cornerSegments, 1, 64);
        SetVerticesDirty();
    }
#endif
}
