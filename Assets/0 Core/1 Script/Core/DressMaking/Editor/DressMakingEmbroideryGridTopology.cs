#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework;

/// <summary>把折线交点组成的平面图转换为闭合网格单元。</summary>
internal static class DressMakingEmbroideryGridTopology
{
    const float Epsilon = 0.0001f;
    const float AreaEpsilon = 0.00001f;

    internal static bool GenerateRegions(
        DressMakingEmbroideryLevelData level,
        out int generatedCount,
        out string error)
    {
        generatedCount = 0;
        error = string.Empty;
        List<SourceSegment> sourceSegments = GetSourceSegments(level.GridLines);
        if (sourceSegments.Count < 3)
        {
            error = "线网至少需要 3 条有效线段。";
            return false;
        }

        AddIntersections(sourceSegments);
        BuildGraph(
            sourceSegments,
            out List<Vector2> vertices,
            out List<Edge> edges,
            out List<List<int>> neighbours);
        List<List<Vector2>> faces = GetFaces(vertices, edges, neighbours, level.GridLines);
        if (faces.Count == 0)
        {
            error = "没有检测到闭合单元。请确保分割线相交，并与外轮廓形成闭合区域。";
            return false;
        }

        level.regions = BuildRegions(level.Regions, faces);
        generatedCount = level.regions.Count;
        return true;
    }

    internal static int ImportLinesFromRegions(DressMakingEmbroideryLevelData level)
    {
        var edges = new Dictionary<SegmentKey, (Vector2 Start, Vector2 End)>();
        for (int i = 0; i < level.Regions.Count; i++)
        {
            DressMakingEmbroideryRegionData region = level.Regions[i];
            if (region == null)
                continue;
            // 迁移时使用原始锚点。旧版贝塞尔边缘的逐边采样会让同一共享边产生微小偏差，
            // 从而在交点拓扑中形成不应存在的狭长单元。
            List<Vector2> points = region.boundaryPoints;
            for (int j = 0; j < points.Count; j++)
            {
                Vector2 start = points[j];
                Vector2 end = points[(j + 1) % points.Count];
                SegmentKey key = new(start, end);
                if (!edges.ContainsKey(key))
                    edges.Add(key, (start, end));
            }
        }

        level.gridLines = new List<DressMakingEmbroideryGridLineData>(edges.Count);
        long id = 1;
        foreach ((Vector2 start, Vector2 end) in edges.Values)
        {
            level.gridLines.Add(new DressMakingEmbroideryGridLineData
            {
                id = id++,
                points = new List<Vector2> { start, end },
            });
        }

        return level.gridLines.Count;
    }

    static List<SourceSegment> GetSourceSegments(
        IReadOnlyList<DressMakingEmbroideryGridLineData> lines)
    {
        var result = new List<SourceSegment>();
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            DressMakingEmbroideryGridLineData line = lines[lineIndex];
            if (line?.points == null || line.points.Count < 2)
                continue;

            line.EnsureBezierControls();
            for (int segmentIndex = 0; segmentIndex < line.SegmentCount; segmentIndex++)
            {
                int samples = line.UseBezier ? line.CurveSegments : 1;
                Vector2 start = line.EvaluateSegment(segmentIndex, 0f);
                for (int sample = 1; sample <= samples; sample++)
                {
                    Vector2 end = line.EvaluateSegment(segmentIndex, sample / (float)samples);
                    if ((end - start).sqrMagnitude > Epsilon * Epsilon)
                        result.Add(new SourceSegment(lineIndex, segmentIndex, start, end));
                    start = end;
                }
            }
        }
        return result;
    }

    static void AddIntersections(List<SourceSegment> segments)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            for (int j = i + 1; j < segments.Count; j++)
            {
                if (!GetIntersection(
                        segments[i].Start,
                        segments[i].End,
                        segments[j].Start,
                        segments[j].End,
                        out float firstT,
                        out float secondT))
                    continue;
                segments[i].Splits.Add(firstT);
                segments[j].Splits.Add(secondT);
            }
        }
    }

    static bool GetIntersection(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd,
        out float firstT,
        out float secondT)
    {
        Vector2 first = firstEnd - firstStart;
        Vector2 second = secondEnd - secondStart;
        float cross = Cross(first, second);
        if (Mathf.Abs(cross) <= Epsilon)
        {
            firstT = secondT = 0f;
            return false;
        }

        Vector2 offset = secondStart - firstStart;
        firstT = Cross(offset, second) / cross;
        secondT = Cross(offset, first) / cross;
        return firstT >= -Epsilon && firstT <= 1f + Epsilon
            && secondT >= -Epsilon && secondT <= 1f + Epsilon;
    }

    static void BuildGraph(
        List<SourceSegment> sourceSegments,
        out List<Vector2> vertices,
        out List<Edge> edges,
        out List<List<int>> neighbours)
    {
        vertices = new List<Vector2>();
        edges = new List<Edge>();
        var vertexTable = new Dictionary<PointKey, int>();
        var edgeTable = new HashSet<Edge>();
        for (int i = 0; i < sourceSegments.Count; i++)
        {
            SourceSegment source = sourceSegments[i];
            source.Splits.Sort();
            for (int j = source.Splits.Count - 1; j > 0; j--)
            {
                if (Mathf.Abs(source.Splits[j] - source.Splits[j - 1]) <= Epsilon)
                    source.Splits.RemoveAt(j);
            }

            for (int j = 1; j < source.Splits.Count; j++)
            {
                Vector2 start = Vector2.Lerp(source.Start, source.End, source.Splits[j - 1]);
                Vector2 end = Vector2.Lerp(source.Start, source.End, source.Splits[j]);
                int first = GetVertex(start, vertices, vertexTable);
                int second = GetVertex(end, vertices, vertexTable);
                if (first == second)
                    continue;

                Edge edge = new(first, second);
                if (edgeTable.Add(edge))
                    edges.Add(edge);
            }
        }

        neighbours = new List<List<int>>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++)
            neighbours.Add(new List<int>());
        for (int i = 0; i < edges.Count; i++)
        {
            neighbours[edges[i].First].Add(edges[i].Second);
            neighbours[edges[i].Second].Add(edges[i].First);
        }

        List<Vector2> vertexValues = vertices;
        for (int i = 0; i < neighbours.Count; i++)
        {
            int vertex = i;
            neighbours[i].Sort((a, b) =>
                Mathf.Atan2(
                    vertexValues[a].y - vertexValues[vertex].y,
                    vertexValues[a].x - vertexValues[vertex].x)
                .CompareTo(Mathf.Atan2(
                    vertexValues[b].y - vertexValues[vertex].y,
                    vertexValues[b].x - vertexValues[vertex].x)));
        }
    }

    static int GetVertex(
        Vector2 point,
        List<Vector2> vertices,
        Dictionary<PointKey, int> table)
    {
        PointKey key = new(point);
        if (table.TryGetValue(key, out int index))
            return index;
        index = vertices.Count;
        vertices.Add(point);
        table.Add(key, index);
        return index;
    }

    static List<List<Vector2>> GetFaces(
        List<Vector2> vertices,
        List<Edge> edges,
        List<List<int>> neighbours,
        IReadOnlyList<DressMakingEmbroideryGridLineData> lines)
    {
        var result = new List<List<Vector2>>();
        var visited = new HashSet<DirectedEdge>();
        List<List<Vector2>> boundaries = GetBoundaries(lines);
        for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
        {
            AddFace(edges[edgeIndex].First, edges[edgeIndex].Second);
            AddFace(edges[edgeIndex].Second, edges[edgeIndex].First);
        }

        return result;

        void AddFace(int start, int next)
        {
            var polygon = new List<Vector2>();
            int from = start;
            int to = next;
            int guard = edges.Count * 2 + 2;
            while (guard-- > 0)
            {
                DirectedEdge directed = new(from, to);
                if (visited.Contains(directed))
                    return;
                visited.Add(directed);
                polygon.Add(vertices[from]);

                List<int> options = neighbours[to];
                int reverse = options.IndexOf(from);
                if (reverse < 0 || options.Count == 0)
                    return;
                int following = options[(reverse + options.Count - 1) % options.Count];
                from = to;
                to = following;
                if (from == start && to == next)
                    break;
            }

            polygon = Simplify(polygon);
            if (polygon.Count < 3 || SignedArea(polygon) <= AreaEpsilon)
                return;
            Vector2 center = GetCentroid(polygon);
            if (boundaries.Count > 0 && !IsInsideAny(center, boundaries))
                return;
            result.Add(polygon);
        }
    }

    internal static List<Vector2> GetSampledLine(DressMakingEmbroideryGridLineData line)
    {
        var result = new List<Vector2>();
        if (line?.points == null || line.points.Count < 2)
            return result;
        line.EnsureBezierControls();
        for (int segmentIndex = 0; segmentIndex < line.SegmentCount; segmentIndex++)
        {
            int samples = line.UseBezier ? line.CurveSegments : 1;
            if (segmentIndex == 0)
                result.Add(line.EvaluateSegment(segmentIndex, 0f));
            for (int sample = 1; sample <= samples; sample++)
                result.Add(line.EvaluateSegment(segmentIndex, sample / (float)samples));
        }
        if (line.isClosed && result.Count > 1)
            result.RemoveAt(result.Count - 1);
        return result;
    }

    static List<List<Vector2>> GetBoundaries(
        IReadOnlyList<DressMakingEmbroideryGridLineData> lines)
    {
        var boundaries = new List<List<Vector2>>();
        for (int i = 0; i < lines.Count; i++)
        {
            DressMakingEmbroideryGridLineData line = lines[i];
            if (line?.isBoundary == true && line.isClosed && line.points.Count >= 3)
                boundaries.Add(GetSampledLine(line));
        }
        return boundaries;
    }
    static List<Vector2> Simplify(List<Vector2> polygon)
    {
        var result = new List<Vector2>(polygon.Count);
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 previous = polygon[(i + polygon.Count - 1) % polygon.Count];
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            if (Mathf.Abs(Cross(current - previous, next - current)) > Epsilon)
                result.Add(current);
        }

        return result;
    }

    static List<DressMakingEmbroideryRegionData> BuildRegions(
        IReadOnlyList<DressMakingEmbroideryRegionData> oldRegions,
        List<List<Vector2>> faces)
    {
        faces.Sort((a, b) =>
        {
            Vector2 first = GetCentroid(a);
            Vector2 second = GetCentroid(b);
            int row = -first.y.CompareTo(second.y);
            return row != 0 ? row : first.x.CompareTo(second.x);
        });

        var result = new List<DressMakingEmbroideryRegionData>(faces.Count);
        var used = new HashSet<int>();
        long nextId = 1;
        for (int i = 0; i < oldRegions.Count; i++)
        {
            if (oldRegions[i] != null)
                nextId = Math.Max(nextId, oldRegions[i].id + 1);
        }

        for (int i = 0; i < faces.Count; i++)
        {
            List<Vector2> face = faces[i];
            Vector2 center = GetCentroid(face);
            int sourceIndex = GetSourceRegion(oldRegions, used, center, face);
            DressMakingEmbroideryRegionData region = sourceIndex >= 0
                ? CloneMetadata(oldRegions[sourceIndex])
                : new DressMakingEmbroideryRegionData { id = nextId++ };
            if (sourceIndex >= 0)
                used.Add(sourceIndex);
            region.boundaryPoints = face;
            region.bezierPoints.Clear();
            region.neighbourIds.Clear();
            region.isClosed = true;
            if (!EmbroideryGeometry.ContainsPoint(face, region.labelPosition))
                region.labelPosition = center;
            result.Add(region);
        }

        return result;
    }

    static int GetSourceRegion(
        IReadOnlyList<DressMakingEmbroideryRegionData> regions,
        HashSet<int> used,
        Vector2 center,
        IReadOnlyList<Vector2> face)
    {
        int nearest = -1;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < regions.Count; i++)
        {
            DressMakingEmbroideryRegionData region = regions[i];
            if (region == null || used.Contains(i))
                continue;

            List<Vector2> oldFace = region.GetSampledBoundary(8);
            Vector2 oldCenter = GetCentroid(oldFace);
            if (EmbroideryGeometry.ContainsPoint(oldFace, center)
                || EmbroideryGeometry.ContainsPoint(face, oldCenter))
                return i;

            float distance = Vector2.SqrMagnitude(center - oldCenter);
            if (distance < nearestDistance)
            {
                nearest = i;
                nearestDistance = distance;
            }
        }

        return nearestDistance <= 0.04f ? nearest : -1;
    }

    static DressMakingEmbroideryRegionData CloneMetadata(DressMakingEmbroideryRegionData source)
    {
        return new DressMakingEmbroideryRegionData
        {
            id = source.id,
            requiredCount = source.requiredCount,
            quantity = source.quantity,
            fillColor = source.fillColor,
            completedColor = source.completedColor,
            fillTexture = source.fillTexture,
            stitchTileSize = source.stitchTileSize,
            label = source.label,
            labelPosition = source.labelPosition,
            labelFontSize = source.labelFontSize,
            isNumberBlock = source.isNumberBlock,
            fillSpritePath = source.fillSpritePath,
            numberSpritePath = source.numberSpritePath,
            remark = source.remark,
        };
    }

    internal static Vector2 GetCentroid(IReadOnlyList<Vector2> polygon)
    {
        if (polygon == null || polygon.Count == 0)
            return new Vector2(0.5f, 0.5f);
        float area = SignedArea(polygon);
        if (Mathf.Abs(area) <= AreaEpsilon)
        {
            Vector2 average = Vector2.zero;
            for (int i = 0; i < polygon.Count; i++)
                average += polygon[i];
            return average / polygon.Count;
        }

        Vector2 center = Vector2.zero;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 first = polygon[i];
            Vector2 second = polygon[(i + 1) % polygon.Count];
            float cross = Cross(first, second);
            center += (first + second) * cross;
        }

        return center / (6f * area);
    }

    static bool IsInsideAny(Vector2 point, List<List<Vector2>> polygons)
    {
        for (int i = 0; i < polygons.Count; i++)
        {
            if (EmbroideryGeometry.ContainsPoint(polygons[i], point))
                return true;
        }

        return false;
    }

    static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
            area += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
        return area * 0.5f;
    }

    static float Cross(Vector2 first, Vector2 second)
        => first.x * second.y - first.y * second.x;

    sealed class SourceSegment
    {
        public readonly int LineIndex;
        public readonly int SegmentIndex;
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly List<float> Splits = new() { 0f, 1f };

        public SourceSegment(int lineIndex, int segmentIndex, Vector2 start, Vector2 end)
        {
            LineIndex = lineIndex;
            SegmentIndex = segmentIndex;
            Start = start;
            End = end;
        }
    }

    readonly struct PointKey : IEquatable<PointKey>
    {
        readonly int x;
        readonly int y;

        public PointKey(Vector2 point)
        {
            x = Mathf.RoundToInt(point.x / Epsilon);
            y = Mathf.RoundToInt(point.y / Epsilon);
        }

        public bool Equals(PointKey other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is PointKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y);
        public int CompareTo(PointKey other) => x != other.x ? x.CompareTo(other.x) : y.CompareTo(other.y);
    }

    readonly struct SegmentKey : IEquatable<SegmentKey>
    {
        readonly PointKey first;
        readonly PointKey second;

        public SegmentKey(Vector2 start, Vector2 end)
        {
            PointKey a = new(start);
            PointKey b = new(end);
            if (a.CompareTo(b) <= 0)
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

        public bool Equals(SegmentKey other) => first.Equals(other.first) && second.Equals(other.second);
        public override bool Equals(object obj) => obj is SegmentKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(first, second);
    }

    readonly struct Edge : IEquatable<Edge>
    {
        public readonly int First;
        public readonly int Second;

        public Edge(int first, int second)
        {
            First = Mathf.Min(first, second);
            Second = Mathf.Max(first, second);
        }

        public bool Equals(Edge other) => First == other.First && Second == other.Second;
        public override bool Equals(object obj) => obj is Edge other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(First, Second);
    }

    readonly struct DirectedEdge : IEquatable<DirectedEdge>
    {
        readonly int from;
        readonly int to;

        public DirectedEdge(int from, int to)
        {
            this.from = from;
            this.to = to;
        }

        public bool Equals(DirectedEdge other) => from == other.from && to == other.to;
        public override bool Equals(object obj) => obj is DirectedEdge other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(from, to);
    }
}
#endif
