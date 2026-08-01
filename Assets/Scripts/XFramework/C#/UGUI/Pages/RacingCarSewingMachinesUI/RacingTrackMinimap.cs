using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>小地图外观参数。</summary>
[Serializable]
public class RacingTrackMinimapStyle
{
    [LabelText("贴图宽"), PropertyRange(32, 512)]
    public int Width = 160;

    [LabelText("贴图高"), PropertyRange(32, 512)]
    public int Height = 96;

    [LabelText("边距(像素)"), PropertyRange(0, 32)]
    public int Padding = 6;

    [LabelText("线宽(像素)"), PropertyRange(1f, 12f)]
    public float LineWidth = 3f;

    [LabelText("赛道颜色")]
    public Color RoadColor = new Color(0.90f, 0.45f, 0.42f, 1f);

    [LabelText("起跑线颜色")]
    public Color StartLineColor = new Color(0.45f, 0.90f, 0.88f, 1f);

    [LabelText("终点线颜色")]
    [Tooltip("只有开放赛道（未勾「首尾相连」）才有终点线")]
    public Color FinishLineColor = new Color(1f, 0.85f, 0.35f, 1f);

    [LabelText("起跑线长度(倍线宽)"), PropertyRange(1f, 4f)]
    public float StartLineLength = 2.2f;
}

/// <summary>
/// 把 <see cref="RacingTrackRoute"/> 光栅化成一张小地图贴图。
///
/// 刻意不做抗锯齿、不做圆滑：参考图那种块状锯齿线条就是硬阈值填出来的。
/// 用运行时现烘而不是让美术出图，是因为策划改完线路要能立刻看到，
/// 而且不会每条赛道多出一张需要跟线路保持同步的图片资产。
///
/// 车辆光点不烘进贴图——它每帧都动，由 <see cref="RacingTrackMinimapUI"/> 用独立 UI 节点摆位。
/// </summary>
public static class RacingTrackMinimap
{
    /// <summary>烘一张小地图。调用方负责在不用时 Destroy 返回的贴图。</summary>
    public static Texture2D Bake(RacingTrackRoute route, RacingTrackMinimapStyle style)
    {
        if(route == null || !route.IsBaked)
            return null;

        style ??= new RacingTrackMinimapStyle();
        int w = Mathf.Max(8, style.Width);
        int h = Mathf.Max(8, style.Height);

        var px = new Color32[w * h];   // 默认全透明

        float radius = Mathf.Max(0.5f, style.LineWidth * 0.5f);
        BuildTransform(route.Bounds, w, h, style.Padding + radius, out Vector2 scale, out Vector2 offset);

        var pts = route.Points;
        int n = pts.Count;
        Color32 road = style.RoadColor;

        // 开放赛道最后一点是终点，不能连回起点，否则小地图上会凭空多出一条边
        int links = route.IsClosed ? n : n - 1;
        for(int i = 0; i < links; i++)
        {
            Vector2 a = WorldToPixel(pts[i], scale, offset);
            Vector2 b = WorldToPixel(pts[(i + 1) % n], scale, offset);
            DrawSegment(px, w, h, a, b, radius, road);
        }

        // 起跑线：s=0 处垂直于行进方向的一小段，画在赛道之上
        DrawCrossMark(px, w, h, route, style, scale, offset, 0f, radius, style.StartLineColor);

        // 开放赛道另有终点，也标一下，否则玩家不知道跑到哪算完
        if(!route.IsClosed)
            DrawCrossMark(px, w, h, route, style, scale, offset, route.TotalLength, radius, style.FinishLineColor);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = route.name + "_Minimap",
            filterMode = FilterMode.Point,   // 放大后保持硬像素，别糊成一团
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>在里程 s 处画一段垂直于行进方向的横杠（起跑线 / 终点线）。</summary>
    static void DrawCrossMark(Color32[] px, int w, int h, RacingTrackRoute route, RacingTrackMinimapStyle style,
        Vector2 scale, Vector2 offset, float s, float radius, Color32 color)
    {
        Vector2 c = WorldToPixel(route.PositionAt(s), scale, offset);
        float heading = route.HeadingAt(s);
        var normal = new Vector2(-Mathf.Sin(heading), Mathf.Cos(heading));
        // 世界法线到像素法线要跟着缩放走，否则非等比时方向会歪
        normal = new Vector2(normal.x * scale.x, normal.y * scale.y).normalized;

        // 厚度取和赛道一样，才是参考图里那种醒目的方块，而不是一根细丝
        float half = radius * style.StartLineLength;
        DrawSegment(px, w, h, c - normal * half, c + normal * half, radius, color);
    }

    /// <summary>世界坐标 → 贴图内归一化 UV，供车辆光点定位。与 <see cref="Bake"/> 用同一套变换。</summary>
    public static Vector2 WorldToUV(RacingTrackRoute route, RacingTrackMinimapStyle style, Vector2 world)
    {
        style ??= new RacingTrackMinimapStyle();
        int w = Mathf.Max(8, style.Width);
        int h = Mathf.Max(8, style.Height);
        float radius = Mathf.Max(0.5f, style.LineWidth * 0.5f);
        BuildTransform(route.Bounds, w, h, style.Padding + radius, out Vector2 scale, out Vector2 offset);

        Vector2 p = WorldToPixel(world, scale, offset);
        return new Vector2(p.x / w, p.y / h);
    }

    /// <summary>等比映射：包围盒塞进「去掉边距的画布」，保持长宽比居中，赛道形状不会被拉扁。</summary>
    static void BuildTransform(Rect bounds, int w, int h, float pad, out Vector2 scale, out Vector2 offset)
    {
        float innerW = Mathf.Max(1f, w - pad * 2f);
        float innerH = Mathf.Max(1f, h - pad * 2f);
        float sx = bounds.width > 1e-4f ? innerW / bounds.width : 1f;
        float sy = bounds.height > 1e-4f ? innerH / bounds.height : 1f;
        float s = Mathf.Min(sx, sy);

        scale = new Vector2(s, s);
        offset = new Vector2(
            w * 0.5f - (bounds.xMin + bounds.width * 0.5f) * s,
            h * 0.5f - (bounds.yMin + bounds.height * 0.5f) * s);
    }

    static Vector2 WorldToPixel(Vector2 world, Vector2 scale, Vector2 offset)
        => new Vector2(world.x * scale.x + offset.x, world.y * scale.y + offset.y);

    /// <summary>画一段带圆头的粗线段：只遍历该段的包围盒，按到线段的距离硬阈值填色（不做 AA，保留像素锯齿）。</summary>
    static void DrawSegment(Color32[] px, int w, int h, Vector2 a, Vector2 b, float radius, Color32 color)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1f));
        int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1f));
        int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1f));

        Vector2 ab = b - a;
        float lenSq = Mathf.Max(ab.sqrMagnitude, 1e-6f);
        float r2 = radius * radius;

        for(int y = y0; y <= y1; y++)
        {
            for(int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
                Vector2 closest = a + ab * t;
                if((p - closest).sqrMagnitude <= r2)
                    px[y * w + x] = color;
            }
        }
    }
}
