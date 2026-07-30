using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>根据逻辑网格单元的共享边界绘制棕色分段线和外轮廓。</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class DressMakingEmbroideryGridDividerGraphic : MaskableGraphic
    {
        readonly List<List<Vector2>> cells = new();
        DressMakingEmbroideryGridDividerSettings settings = new();

        public void SetCells(
            IReadOnlyList<DressMakingEmbroideryRegionView> regionViews,
            DressMakingEmbroideryGridDividerSettings value)
        {
            cells.Clear();
            for (int i = 0; i < regionViews.Count; i++)
                cells.Add(new List<Vector2>(regionViews[i].Polygon));

            settings = value ?? new DressMakingEmbroideryGridDividerSettings();
            settings.Normalize();
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!settings.enabled)
                return;

            var segments = new Dictionary<SegmentKey, Segment>();
            for (int i = 0; i < cells.Count; i++)
            {
                List<Vector2> cell = cells[i];
                for (int j = 0; j < cell.Count; j++)
                {
                    Vector2 start = cell[j];
                    Vector2 end = cell[(j + 1) % cell.Count];
                    SegmentKey key = new(start, end);
                    if (segments.TryGetValue(key, out Segment segment))
                    {
                        segment.Count++;
                        segments[key] = segment;
                    }
                    else
                    {
                        segments.Add(key, new Segment(start, end));
                    }
                }
            }

            foreach (Segment segment in segments.Values)
            {
                if (segment.Count > 1)
                {
                    AddDashedLine(
                        vertexHelper,
                        segment.Start,
                        segment.End,
                        settings.DividerWidth,
                        settings.dividerColor,
                        settings.DashLength,
                        settings.GapLength);
                }
                else if (settings.drawOuterBorder)
                {
                    AddLineSegment(
                        vertexHelper,
                        segment.Start,
                        segment.End,
                        settings.BorderWidth,
                        settings.borderColor);
                }
            }
        }

        static void AddDashedLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float width,
            Color color,
            float dashLength,
            float gapLength)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.001f)
                return;

            Vector2 direction = delta / length;
            float period = dashLength + gapLength;
            for (float distance = 0f; distance < length; distance += period)
            {
                float dashEnd = Mathf.Min(distance + dashLength, length);
                AddLineSegment(
                    vertexHelper,
                    start + direction * distance,
                    start + direction * dashEnd,
                    width,
                    color);
            }
        }

        static void AddLineSegment(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float width,
            Color color)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            int index = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = start - normal;
            vertexHelper.AddVert(vertex);
            vertex.position = start + normal;
            vertexHelper.AddVert(vertex);
            vertex.position = end + normal;
            vertexHelper.AddVert(vertex);
            vertex.position = end - normal;
            vertexHelper.AddVert(vertex);
            vertexHelper.AddTriangle(index, index + 1, index + 2);
            vertexHelper.AddTriangle(index, index + 2, index + 3);
        }

        readonly struct SegmentKey : IEquatable<SegmentKey>
        {
            const float Precision = 10f;
            readonly Vector2Int first;
            readonly Vector2Int second;

            public SegmentKey(Vector2 start, Vector2 end)
            {
                Vector2Int a = Quantize(start);
                Vector2Int b = Quantize(end);
                if (a.x < b.x || a.x == b.x && a.y <= b.y)
                {
                    first = a;
                    second = b;
                }
                else
                {
                    first = b;
                    second = a;
                }
            }

            public bool Equals(SegmentKey other) => first == other.first && second == other.second;
            public override bool Equals(object obj) => obj is SegmentKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(first, second);

            static Vector2Int Quantize(Vector2 point)
                => new(
                    Mathf.RoundToInt(point.x * Precision),
                    Mathf.RoundToInt(point.y * Precision));
        }

        struct Segment
        {
            public readonly Vector2 Start;
            public readonly Vector2 End;
            public int Count;

            public Segment(Vector2 start, Vector2 end)
            {
                Start = start;
                End = end;
                Count = 1;
            }
        }
    }
}
