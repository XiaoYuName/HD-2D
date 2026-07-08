using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 程序化「圆角方框」UI，用顶点网格直接绘制中空的圆角矩形描边（不填充内部），
/// 任意缩放都清晰，不依赖任何 Sprite 或九宫格图片。
///
/// 是 <see cref="DashedRoundedRect"/> 的简化版：去掉了虚线/弧长采样相关逻辑，
/// 纯色圆角边框场景（选中框、卡片描边等）优先用本组件，开销更低。
///
/// 用法：挂到一个 UI 节点（像 Image 那样），不需要 Sprite；
/// 线条颜色取 Graphic 自带 Color；线宽向内绘制，描边外缘对齐 RectTransform 边界。
/// </summary>
[AddComponentMenu("UI/Rounded Rect (程序化圆角方框)")]
public class RoundedRect : MaskableGraphic
{
    [Title("描边")]
    [LabelText("线宽(px)"), MinValue(0f)]
    [SerializeField] float thickness = 4f;

    [LabelText("圆角半径(px)"), MinValue(0f)]
    [SerializeField] float cornerRadius = 24f;

    [LabelText("每个圆角分段数"), Range(1, 64), Tooltip("越大圆角越圆滑，顶点也越多")]
    [SerializeField] int cornerSegments = 8;

    // 外轮廓路径缓存，跨实例复用避免每帧 GC；仅在单帧同步的 OnPopulateMesh 内使用完即弃，不可重入。
    static readonly List<Vector2> outerPosList = new();
    static readonly List<Vector2> outerNrmList = new();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float w = rect.width, h = rect.height;
        if (w <= 0f || h <= 0f) return;
        if (thickness <= 0f || color.a <= 0f) return;

        float maxInset = Mathf.Min(w, h) * 0.5f;
        float th = Mathf.Min(thickness, maxInset);
        float cr = Mathf.Clamp(cornerRadius, 0f, maxInset);

        BuildOuterPath(rect, cr);
        AddRing(vh, 0f, th, color);
    }

    // 生成闭合圆角矩形外轮廓（4 段圆弧，直边为弧与弧之间的连接段），外缘对齐 RectTransform 边界。
    void BuildOuterPath(Rect rect, float cr)
    {
        outerPosList.Clear();
        outerNrmList.Clear();

        float left = rect.xMin, right = rect.xMax, bottom = rect.yMin, top = rect.yMax;

        // CCW 顺序：右下 到 右上 到 左上 到 左下，弧之间的连接段即为四条直边。
        AddArc(new Vector2(right - cr, bottom + cr), cr, 270f, 360f);
        AddArc(new Vector2(right - cr, top - cr), cr, 0f, 90f);
        AddArc(new Vector2(left + cr, top - cr), cr, 90f, 180f);
        AddArc(new Vector2(left + cr, bottom + cr), cr, 180f, 270f);
    }

    void AddArc(Vector2 centerPos, float radius, float degFrom, float degTo)
    {
        for (int i = 0; i <= cornerSegments; i++)
        {
            float rad = Mathf.Lerp(degFrom, degTo, (float)i / cornerSegments) * Mathf.Deg2Rad;
            Vector2 nrm = new(Mathf.Cos(rad), Mathf.Sin(rad)); // 外法线
            outerPosList.Add(centerPos + nrm * radius);
            outerNrmList.Add(nrm);
        }
    }

    // 外轮廓内缩 [outerInset, innerInset] 之间的闭合三角形带，即描边圆环。
    void AddRing(VertexHelper vh, float outerInset, float innerInset, Color32 col)
    {
        int count = outerPosList.Count;
        int baseIdx = vh.currentVertCount;
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = outerPosList[i];
            Vector2 nrm = outerNrmList[i];
            AddVert(vh, pos - nrm * outerInset, col);
            AddVert(vh, pos - nrm * innerInset, col);
        }

        for (int i = 0; i < count; i++)
        {
            int outer0 = baseIdx + i * 2, inner0 = outer0 + 1;
            int next = (i + 1) % count;
            int outer1 = baseIdx + next * 2, inner1 = outer1 + 1;
            vh.AddTriangle(outer0, inner0, outer1);
            vh.AddTriangle(inner0, inner1, outer1);
        }
    }

    static void AddVert(VertexHelper vh, Vector2 pos, Color32 col)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = pos;
        v.color = col;
        v.uv0 = Vector2.zero; // 无贴图，取白图 (0,0) 恒为白
        vh.AddVert(v);
    }

    void Reset()
    {
        raycastTarget = false; // 纯装饰性描边，默认不参与射线检测
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
