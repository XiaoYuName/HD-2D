#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework;

/// <summary>刺绣工作台画布。只负责绘制、命中和输入事件，不直接修改配置资产。</summary>
internal sealed class DressMakingEmbroideryCanvasElement : VisualElement
{
    private const float AnchorRadius = 8f;
    private const float ControlRadius = 7f;
    private const float HitDistance = 10f;

    private static readonly Color[] RegionPalette =
    {
        new(0.95f, 0.58f, 0.38f, 0.82f),
        new(0.35f, 0.72f, 0.94f, 0.82f),
        new(0.55f, 0.82f, 0.45f, 0.82f),
        new(0.82f, 0.52f, 0.91f, 0.82f),
        new(0.98f, 0.78f, 0.34f, 0.82f),
        new(0.35f, 0.82f, 0.76f, 0.82f),
        new(0.92f, 0.46f, 0.64f, 0.82f),
        new(0.62f, 0.68f, 0.96f, 0.82f),
    };

    private enum DragMode
    {
        None,
        Anchor,
        BezierControl,
        Line,
        Label,
    }

    private DressMakingEmbroideryLevelData level;
    private DressMakingEmbroideryGridLineData selectedLine;
    private int selectedRegionIndex = -1;
    private int selectedLineIndex = -1;
    private int draggedIndex = -1;
    private int pointerId = -1;
    private DragMode dragMode;
    private Vector2 previousPointer;
    private readonly List<Label> labels = new();

    public event Action<int> RegionClicked;
    public event Action<int> GridLineClicked;
    public event Action<string> DragStarted;
    public event Action DragEnded;
    public event Action<int, int, Vector2> GridLinePointChanged;
    public event Action<int, int, Vector2> BezierControlChanged;
    public event Action<int, Vector2> GridLineTranslated;
    public event Action<int, Vector2> LabelPositionChanged;
    public event Action<Vector2> AddLinePointRequested;

    public DressMakingEmbroideryCanvasElement()
    {
        pickingMode = PickingMode.Position;
        focusable = true;
        generateVisualContent += Paint;
        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
        RegisterCallback<GeometryChangedEvent>(_ => RefreshLabels());
    }

    public void SetData(
        DressMakingEmbroideryLevelData value,
        DressMakingEmbroideryRegionData selectedRegion,
        DressMakingEmbroideryGridLineData selectedGridLine)
    {
        level = value;
        selectedLine = selectedGridLine;
        selectedRegionIndex = level == null || selectedRegion == null
            ? -1
            : level.Regions.IndexOf(selectedRegion);
        selectedLineIndex = level == null || selectedLine == null
            ? -1
            : level.GridLines.IndexOf(selectedLine);
        style.backgroundColor = level?.backgroundColor
            ?? new Color(0.09f, 0.11f, 0.12f, 1f);
        RefreshLabels();
        MarkDirtyRepaint();
    }

    public void RefreshLabels()
    {
        for (int i = 0; i < labels.Count; i++)
            labels[i].RemoveFromHierarchy();
        labels.Clear();
        if (level == null || contentRect.width <= 1f)
            return;

        float scale = contentRect.width / Mathf.Max(1f, level.CanvasSize.x);
        for (int i = 0; i < level.Regions.Count; i++)
        {
            DressMakingEmbroideryRegionData region = level.Regions[i];
            if (region == null)
                continue;
            string text = string.IsNullOrEmpty(region.label)
                ? region.isNumberBlock ? region.RequiredCount.ToString() : string.Empty
                : region.label;
            if (string.IsNullOrEmpty(text))
                continue;

            float fontSize = Mathf.Max(8f, region.LabelFontSize * scale);
            Vector2 position = ToCanvas(region.labelPosition);
            var label = new Label(text)
            {
                pickingMode = PickingMode.Ignore,
            };
            label.style.position = Position.Absolute;
            label.style.left = position.x - fontSize;
            label.style.top = position.y - fontSize * 0.72f;
            label.style.width = fontSize * 2f;
            label.style.height = fontSize * 1.45f;
            label.style.fontSize = fontSize;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = region.isNumberBlock
                ? new Color(1f, 0.88f, 0.46f)
                : Color.white;
            label.style.unityTextOutlineColor = new Color(0.08f, 0.08f, 0.09f, 1f);
            label.style.unityTextOutlineWidth = 1.5f;
            Add(label);
            labels.Add(label);
        }
    }

    private void Paint(MeshGenerationContext context)
    {
        Rect rect = contentRect;
        if (rect.width < 2f || rect.height < 2f)
            return;

        Painter2D painter = context.painter2D;
        FillRect(painter, rect, level?.backgroundColor ?? new Color(0.09f, 0.11f, 0.12f));
        if (level == null)
            return;

        for (int i = 0; i < level.Regions.Count; i++)
            DrawRegion(painter, level.Regions[i], i);
        for (int i = 0; i < level.GridLines.Count; i++)
            DrawLine(painter, level.GridLines[i], i);
        DrawSelectedHandles(painter);
    }

    private static void FillRect(Painter2D painter, Rect rect, Color color)
    {
        painter.fillColor = color;
        painter.BeginPath();
        painter.MoveTo(Vector2.zero);
        painter.LineTo(new Vector2(rect.width, 0f));
        painter.LineTo(new Vector2(rect.width, rect.height));
        painter.LineTo(new Vector2(0f, rect.height));
        painter.ClosePath();
        painter.Fill();
    }

    private void DrawRegion(
        Painter2D painter,
        DressMakingEmbroideryRegionData region,
        int index)
    {
        if (region?.boundaryPoints == null || region.boundaryPoints.Count < 3)
            return;
        List<Vector2> points = region.GetSampledBoundary(10);
        Color preview = RegionPalette[index % RegionPalette.Length];
        painter.fillColor = Color.Lerp(region.fillColor, preview, 0.68f);
        BeginPolygon(painter, points);
        painter.Fill();

        bool selected = index == selectedRegionIndex;
        if (selected)
        {
            painter.strokeColor = new Color(0.04f, 0.05f, 0.06f, 1f);
            painter.lineWidth = 7f;
            BeginPolygon(painter, points);
            painter.Stroke();
        }
        painter.strokeColor = selected ? Color.white : new Color(1f, 1f, 1f, 0.62f);
        painter.lineWidth = selected ? 3.5f : 1.2f;
        BeginPolygon(painter, points);
        painter.Stroke();
    }

    private void BeginPolygon(Painter2D painter, IReadOnlyList<Vector2> points)
    {
        painter.BeginPath();
        painter.MoveTo(ToCanvas(points[0]));
        for (int i = 1; i < points.Count; i++)
            painter.LineTo(ToCanvas(points[i]));
        painter.ClosePath();
    }

    private void DrawLine(
        Painter2D painter,
        DressMakingEmbroideryGridLineData line,
        int index)
    {
        List<Vector2> points = DressMakingEmbroideryGridTopology.GetSampledLine(line);
        if (points.Count < 2)
            return;
        bool selected = index == selectedLineIndex;
        bool closed = line.isClosed;
        if (selected)
        {
            DrawPolyline(painter, points, closed, new Color(0.02f, 0.03f, 0.04f, 1f), 8f);
            DrawPolyline(painter, points, closed, Color.white, 4f);
            DrawPolyline(painter, points, closed, new Color(0.18f, 0.86f, 1f, 1f), 2f);
            return;
        }

        Color color = line.isBoundary
            ? level.GridDivider.borderColor
            : RegionPalette[(index + 2) % RegionPalette.Length];
        DrawPolyline(
            painter,
            points,
            closed,
            new Color(color.r, color.g, color.b, 0.95f),
            line.isBoundary ? level.GridDivider.BorderWidth : level.GridDivider.DividerWidth);
    }

    private void DrawPolyline(
        Painter2D painter,
        IReadOnlyList<Vector2> points,
        bool closed,
        Color color,
        float width)
    {
        painter.strokeColor = color;
        painter.lineWidth = width;
        painter.BeginPath();
        painter.MoveTo(ToCanvas(points[0]));
        for (int i = 1; i < points.Count; i++)
            painter.LineTo(ToCanvas(points[i]));
        if (closed)
            painter.ClosePath();
        painter.Stroke();
    }

    private void DrawSelectedHandles(Painter2D painter)
    {
        if (selectedLine?.points == null)
            return;
        selectedLine.EnsureBezierControls();
        if (selectedLine.UseBezier)
        {
            painter.strokeColor = new Color(1f, 0.68f, 0.18f, 0.72f);
            painter.lineWidth = 1.2f;
            for (int i = 0; i < selectedLine.SegmentCount; i++)
            {
                Vector2 control = ToCanvas(selectedLine.BezierControls[i]);
                painter.BeginPath();
                painter.MoveTo(ToCanvas(selectedLine.points[i]));
                painter.LineTo(control);
                painter.LineTo(ToCanvas(selectedLine.points[(i + 1) % selectedLine.points.Count]));
                painter.Stroke();
                DrawDiamond(painter, control, ControlRadius, new Color(1f, 0.67f, 0.16f));
            }
        }

        for (int i = 0; i < selectedLine.points.Count; i++)
            DrawDiamond(painter, ToCanvas(selectedLine.points[i]), AnchorRadius, Color.white);
    }

    private static void DrawDiamond(Painter2D painter, Vector2 center, float radius, Color color)
    {
        painter.fillColor = new Color(0.03f, 0.04f, 0.05f, 1f);
        DrawDiamondPath(painter, center, radius + 3f);
        painter.Fill();
        painter.fillColor = color;
        DrawDiamondPath(painter, center, radius);
        painter.Fill();
    }

    private static void DrawDiamondPath(Painter2D painter, Vector2 center, float radius)
    {
        painter.BeginPath();
        painter.MoveTo(center + new Vector2(-radius, 0f));
        painter.LineTo(center + new Vector2(0f, -radius));
        painter.LineTo(center + new Vector2(radius, 0f));
        painter.LineTo(center + new Vector2(0f, radius));
        painter.ClosePath();
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (level == null || evt.button != 0)
            return;
        Focus();
        Vector2 normalized = ToNormalized(evt.localPosition);

        int labelIndex = FindLabel(evt.localPosition);
        if (labelIndex >= 0)
        {
            RegionClicked?.Invoke(labelIndex);
            BeginDrag(evt.pointerId, DragMode.Label, labelIndex, normalized, "移动刺绣数字");
            evt.StopPropagation();
            return;
        }

        if (selectedLine != null)
        {
            int anchor = FindPoint(selectedLine.points, evt.localPosition, AnchorRadius + 5f);
            if (anchor >= 0)
            {
                BeginDrag(evt.pointerId, DragMode.Anchor, anchor, normalized, "移动曲线锚点");
                evt.StopPropagation();
                return;
            }
            if (selectedLine.UseBezier)
            {
                int control = FindPoint(
                    selectedLine.BezierControls,
                    evt.localPosition,
                    ControlRadius + 5f);
                if (control >= 0)
                {
                    BeginDrag(evt.pointerId, DragMode.BezierControl, control, normalized, "移动贝塞尔控制柄");
                    evt.StopPropagation();
                    return;
                }
            }
        }

        int lineIndex = HitGridLine(evt.localPosition);
        if (lineIndex >= 0)
        {
            GridLineClicked?.Invoke(lineIndex);
            BeginDrag(evt.pointerId, DragMode.Line, lineIndex, normalized, "移动整条分割线");
            evt.StopPropagation();
            return;
        }

        int regionIndex = HitRegion(normalized);
        if (regionIndex >= 0)
        {
            RegionClicked?.Invoke(regionIndex);
            evt.StopPropagation();
            return;
        }

        if (evt.shiftKey && selectedLine != null)
        {
            AddLinePointRequested?.Invoke(normalized);
            evt.StopPropagation();
        }
    }

    private void BeginDrag(
        int capturedPointerId,
        DragMode mode,
        int index,
        Vector2 normalized,
        string undoName)
    {
        pointerId = capturedPointerId;
        dragMode = mode;
        draggedIndex = index;
        previousPointer = normalized;
        DragStarted?.Invoke(undoName);
        this.CapturePointer(pointerId);
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (pointerId != evt.pointerId || dragMode == DragMode.None)
            return;
        Vector2 value = ToNormalized(evt.localPosition);
        switch (dragMode)
        {
            case DragMode.Anchor:
                GridLinePointChanged?.Invoke(selectedLineIndex, draggedIndex, value);
                break;
            case DragMode.BezierControl:
                BezierControlChanged?.Invoke(selectedLineIndex, draggedIndex, value);
                break;
            case DragMode.Line:
                GridLineTranslated?.Invoke(draggedIndex, value - previousPointer);
                previousPointer = value;
                break;
            case DragMode.Label:
                if (draggedIndex >= 0 && draggedIndex < level.Regions.Count)
                    LabelPositionChanged?.Invoke(
                        draggedIndex,
                        ClampPointToRegion(level.Regions[draggedIndex], value));
                break;
        }
        MarkDirtyRepaint();
        RefreshLabels();
        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (pointerId != evt.pointerId)
            return;
        this.ReleasePointer(pointerId);
        EndDrag();
        evt.StopPropagation();
    }

    private void EndDrag()
    {
        if (dragMode == DragMode.None)
            return;
        pointerId = -1;
        draggedIndex = -1;
        dragMode = DragMode.None;
        DragEnded?.Invoke();
    }

    private int FindLabel(Vector2 localPoint)
    {
        float scale = contentRect.width / Mathf.Max(1f, level.CanvasSize.x);
        for (int i = level.Regions.Count - 1; i >= 0; i--)
        {
            DressMakingEmbroideryRegionData region = level.Regions[i];
            if (region == null || !region.isNumberBlock && string.IsNullOrEmpty(region.label))
                continue;
            float radius = Mathf.Max(12f, region.LabelFontSize * scale * 0.75f);
            if (Vector2.Distance(localPoint, ToCanvas(region.labelPosition)) <= radius)
                return i;
        }
        return -1;
    }

    private int FindPoint(IReadOnlyList<Vector2> points, Vector2 localPoint, float radius)
    {
        if (points == null)
            return -1;
        for (int i = points.Count - 1; i >= 0; i--)
        {
            if (Vector2.Distance(ToCanvas(points[i]), localPoint) <= radius)
                return i;
        }
        return -1;
    }

    private int HitGridLine(Vector2 localPoint)
    {
        for (int i = level.GridLines.Count - 1; i >= 0; i--)
        {
            List<Vector2> points =
                DressMakingEmbroideryGridTopology.GetSampledLine(level.GridLines[i]);
            int segmentCount = points.Count - 1 + (level.GridLines[i].isClosed ? 1 : 0);
            for (int j = 0; j < segmentCount; j++)
            {
                if (EmbroideryGeometry.DistanceToSegment(
                        localPoint,
                        ToCanvas(points[j]),
                        ToCanvas(points[(j + 1) % points.Count])) <= HitDistance)
                    return i;
            }
        }
        return -1;
    }

    private int HitRegion(Vector2 normalized)
    {
        for (int i = level.Regions.Count - 1; i >= 0; i--)
        {
            DressMakingEmbroideryRegionData region = level.Regions[i];
            if (region != null &&
                EmbroideryGeometry.ContainsPoint(region.GetSampledBoundary(8), normalized))
                return i;
        }
        return -1;
    }

    private Vector2 ToNormalized(Vector2 local)
        => new(
            Mathf.Clamp01(contentRect.width <= 0f ? 0.5f : local.x / contentRect.width),
            Mathf.Clamp01(contentRect.height <= 0f ? 0.5f : 1f - local.y / contentRect.height));

    private Vector2 ToCanvas(Vector2 normalized)
        => new(normalized.x * contentRect.width, (1f - normalized.y) * contentRect.height);

    private static Vector2 ClampPointToRegion(
        DressMakingEmbroideryRegionData region,
        Vector2 value)
    {
        value = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        List<Vector2> polygon = region.GetSampledBoundary(10);
        if (EmbroideryGeometry.ContainsPoint(polygon, value))
            return value;

        Vector2 closest = region.labelPosition;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 start = polygon[i];
            Vector2 end = polygon[(i + 1) % polygon.Count];
            Vector2 direction = end - start;
            float t = direction.sqrMagnitude <= Mathf.Epsilon
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(value - start, direction) / direction.sqrMagnitude);
            Vector2 candidate = start + direction * t;
            float distance = Vector2.SqrMagnitude(value - candidate);
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            closest = candidate;
        }
        return Vector2.Lerp(
            closest,
            DressMakingEmbroideryGridTopology.GetCentroid(polygon),
            0.01f);
    }
}
#endif
