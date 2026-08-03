using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace XFramework
{
    /// <summary>
    /// 单次拖动路径的校验结果。规则层不依赖场景对象，便于编辑器和运行时复用。
    /// </summary>
    public readonly struct EmbroideryPathValidation
    {
        public readonly bool IsValid;
        public readonly int RequiredCount;
        public readonly int NumberRegionIndex;
        public readonly string Error;

        public EmbroideryPathValidation(bool isValid, int requiredCount, int numberRegionIndex, string error)
        {
            IsValid = isValid;
            RequiredCount = requiredCount;
            NumberRegionIndex = numberRegionIndex;
            Error = error;
        }

        public static EmbroideryPathValidation Invalid(string error)
            => new(false, 0, -1, error);

        public static EmbroideryPathValidation Valid(int requiredCount, int numberRegionIndex)
            => new(true, requiredCount, numberRegionIndex, string.Empty);
    }

    /// <summary>
    /// 规则层使用的区域快照，避免把 Unity UI 状态混入路径校验。
    /// </summary>
    public readonly struct EmbroideryPathRegion
    {
        public readonly int RequiredCount;
        public readonly int Quantity;
        public readonly bool IsNumberBlock;
        public readonly bool IsFilled;

        public EmbroideryPathRegion(
            int requiredCount,
            int quantity,
            bool isNumberBlock,
            bool isFilled)
        {
            RequiredCount = requiredCount;
            Quantity = quantity;
            IsNumberBlock = isNumberBlock;
            IsFilled = isFilled;
        }

        public int Number => RequiredCount > 0 ? RequiredCount : Quantity;
    }

    /// <summary>
    /// 刺绣拖动规则：
    /// 1. 路径中的区域不能重复，也不能经过已完成区域；
    /// 2. 必须经过一个数字块，数字决定本次应经过的块数；
    /// 3. 默认数字块本身计入块数；
    /// </summary>
    public static class EmbroideryPathRuleEngine
    {
        public static EmbroideryPathValidation ValidatePath(
            IReadOnlyList<EmbroideryPathRegion> regions,
            IReadOnlyList<int> path,
            bool includeNumberBlockInCount = true,
            bool countByQuantity = false)
        {
            if (regions == null || path == null || path.Count == 0)
            {
                return EmbroideryPathValidation.Invalid("empty-path");
            }

            int numberRegionIndex = -1;
            int requiredCount = 0;
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < path.Count; i++)
            {
                int regionIndex = path[i];
                if (regionIndex < 0 || regionIndex >= regions.Count)
                {
                    return EmbroideryPathValidation.Invalid("region-index");
                }

                if (!usedIndices.Add(regionIndex))
                {
                    return EmbroideryPathValidation.Invalid("region-repeated");
                }

                EmbroideryPathRegion region = regions[regionIndex];
                if (region.IsFilled)
                {
                    return EmbroideryPathValidation.Invalid("region-filled");
                }

                if (region.IsNumberBlock)
                {
                    if (numberRegionIndex >= 0)
                        return EmbroideryPathValidation.Invalid("multiple-number-blocks");
                    numberRegionIndex = regionIndex;
                    requiredCount = region.Number;
                }
            }

            if (numberRegionIndex < 0)
            {
                return EmbroideryPathValidation.Invalid("number-block-required");
            }

            if (requiredCount <= 0)
            {
                return EmbroideryPathValidation.Invalid("number-invalid");
            }

            int countedRegions = 0;
            for (int i = 0; i < path.Count; i++)
            {
                int quantity = countByQuantity ? Mathf.Max(1, regions[path[i]].Quantity) : 1;
                if (!includeNumberBlockInCount && path[i] == numberRegionIndex)
                    continue;
                countedRegions += quantity;
            }

            if (countedRegions != requiredCount)
            {
                return EmbroideryPathValidation.Invalid("count-mismatch");
            }

            return EmbroideryPathValidation.Valid(requiredCount, numberRegionIndex);
        }
    }

    /// <summary>多边形几何工具：用于区域命中和拖动线段采样。</summary>
    public static class EmbroideryGeometry
    {
        public static bool ContainsPoint(IReadOnlyList<Vector2> points, Vector2 point)
        {
            if (points == null || points.Count < 3)
            {
                return false;
            }

            bool inside = false;
            int previous = points.Count - 1;
            for (int current = 0; current < points.Count; previous = current++)
            {
                Vector2 a = points[current];
                Vector2 b = points[previous];
                bool crosses = (a.y > point.y) != (b.y > point.y);
                if (crosses && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 direction = end - start;
            float lengthSquared = direction.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - start, direction) / lengthSquared);
            return Vector2.Distance(point, start + direction * t);
        }

        public static bool ArePolygonsAdjacent(
            IReadOnlyList<Vector2> first,
            IReadOnlyList<Vector2> second,
            float tolerance,
            float minimumSharedLength = 4f)
        {
            if (first == null || second == null || first.Count < 2 || second.Count < 2)
                return false;

            tolerance = Mathf.Max(0f, tolerance);
            minimumSharedLength = Mathf.Max(0f, minimumSharedLength);
            for (int i = 0; i < first.Count; i++)
            {
                Vector2 a0 = first[i];
                Vector2 a1 = first[(i + 1) % first.Count];
                for (int j = 0; j < second.Count; j++)
                {
                    Vector2 b0 = second[j];
                    Vector2 b1 = second[(j + 1) % second.Count];
                    if (ShareEdge(a0, a1, b0, b1, tolerance, minimumSharedLength))
                        return true;
                }
            }

            return false;
        }

        static bool ShareEdge(
            Vector2 a0,
            Vector2 a1,
            Vector2 b0,
            Vector2 b1,
            float tolerance,
            float minimumSharedLength)
        {
            Vector2 aDirection = a1 - a0;
            Vector2 bDirection = b1 - b0;
            float aLength = aDirection.magnitude;
            float bLength = bDirection.magnitude;
            if (aLength <= Mathf.Epsilon || bLength <= Mathf.Epsilon)
                return false;

            Vector2 axis = aDirection / aLength;
            Vector2 otherAxis = bDirection / bLength;
            if (Mathf.Abs(Cross(axis, otherAxis)) > 0.26f)
                return false;

            float distance = Mathf.Min(
                Mathf.Min(DistanceToSegment(a0, b0, b1), DistanceToSegment(a1, b0, b1)),
                Mathf.Min(DistanceToSegment(b0, a0, a1), DistanceToSegment(b1, a0, a1)));
            if (distance > tolerance)
                return false;

            float bProjection0 = Vector2.Dot(b0 - a0, axis);
            float bProjection1 = Vector2.Dot(b1 - a0, axis);
            float overlap = Mathf.Min(aLength, Mathf.Max(bProjection0, bProjection1))
                            - Mathf.Max(0f, Mathf.Min(bProjection0, bProjection1));
            return overlap >= minimumSharedLength;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>
        /// 将 p0 + 每组三个控制点的三次 Bezier 曲线采样为多边形。
        /// 不符合 (3n+1) 格式时返回原始点，兼容编辑器只保存边界点的旧数据。
        /// </summary>
        public static List<Vector2> SampleBezier(IReadOnlyList<Vector2> bezierPoints, int segmentsPerCurve = 8)
        {
            if (bezierPoints == null || bezierPoints.Count < 4 || (bezierPoints.Count - 1) % 3 != 0)
            {
                return bezierPoints == null ? new List<Vector2>() : new List<Vector2>(bezierPoints);
            }

            int segmentCount = (bezierPoints.Count - 1) / 3;
            int sampleCount = Mathf.Max(2, segmentsPerCurve);
            var result = new List<Vector2>(segmentCount * sampleCount + 1) { bezierPoints[0] };
            for (int segment = 0; segment < segmentCount; segment++)
            {
                Vector2 p0 = bezierPoints[segment * 3];
                Vector2 p1 = bezierPoints[segment * 3 + 1];
                Vector2 p2 = bezierPoints[segment * 3 + 2];
                Vector2 p3 = bezierPoints[segment * 3 + 3];
                for (int i = 1; i <= sampleCount; i++)
                {
                    float t = i / (float)sampleCount;
                    float inverse = 1f - t;
                    result.Add(
                        inverse * inverse * inverse * p0
                        + 3f * inverse * inverse * t * p1
                        + 3f * inverse * t * t * p2
                        + t * t * t * p3);
                }
            }

            return result;
        }

        public static Rect GetBounds(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
            {
                return new Rect();
            }

            float minX = points[0].x;
            float maxX = minX;
            float minY = points[0].y;
            float maxY = minY;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 point = points[i];
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }

    /// <summary>
    /// UGUI 多边形 Graphic。区域由配置点直接生成，不依赖图片尺寸，适合任意形状刺绣。
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public abstract class DressMakingEmbroideryPolygonGraphicBase : MaskableGraphic
    {
        readonly List<Vector2> points = new();
        readonly List<Vector2> firstClip = new();
        readonly List<Vector2> secondClip = new();
        readonly List<int> triangles = new();
        Texture texture;
        float textureTileSize = 32f;
        float revealProgress = 1f;
        float revealFeather = 8f;
        float stitchEdgeWidth;
        float stitchEdgeNoise;
        Vector2 revealDirection = Vector2.right;
        Vector2 revealOrigin;
        bool hasRevealOrigin;

        public IReadOnlyList<Vector2> Points => points;
        public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;

        public void SetPolygon(IReadOnlyList<Vector2> value)
        {
            points.Clear();
            if (value != null)
                points.AddRange(value);
            SetVerticesDirty();
        }

        public void SetTexture(Texture value, float tileSize)
        {
            texture = value;
            textureTileSize = Mathf.Max(4f, tileSize);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                textureTileSize *= Mathf.Max(1f, Mathf.Max(texture.width, texture.height) / 64f);
            }
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void SetStitchEffect(float feather, float edgeWidth, float edgeNoise)
        {
            revealFeather = Mathf.Max(0.5f, feather);
            stitchEdgeWidth = Mathf.Max(0f, edgeWidth);
            stitchEdgeNoise = Mathf.Max(0f, edgeNoise);
            SetVerticesDirty();
        }

        public void SetReveal(float progress, Vector2 direction)
        {
            revealProgress = Mathf.Clamp01(progress);
            if (direction.sqrMagnitude > 0.0001f)
                revealDirection = direction.normalized;
            SetVerticesDirty();
        }

        public void SetRevealOrigin(Vector2 origin)
        {
            revealOrigin = origin;
            hasRevealOrigin = true;
            SetVerticesDirty();
        }

        public void ClearRevealOrigin()
        {
            hasRevealOrigin = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (points.Count < 3 || revealProgress <= 0f)
                return;

            Vector2 direction = revealDirection.sqrMagnitude > 0.0001f
                ? revealDirection.normalized
                : Vector2.right;
            GetProjectionRange(points, direction, out float minimum, out float maximum);
            if (revealProgress < 0.999f)
            {
                if (hasRevealOrigin)
                {
                    AddOriginReveal(vertexHelper, direction);
                    return;
                }
                float outerThreshold = Mathf.Lerp(minimum, maximum, revealProgress);
                float innerThreshold = outerThreshold - revealFeather;
                ClipProjection(points, firstClip, direction, innerThreshold, true);
                AddPolygon(vertexHelper, firstClip, direction, innerThreshold, outerThreshold, false);

                ClipProjection(points, firstClip, direction, outerThreshold, true);
                ClipProjection(firstClip, secondClip, direction, innerThreshold, false);
                AddPolygon(vertexHelper, secondClip, direction, innerThreshold, outerThreshold, true);
                return;
            }

            AddPolygon(vertexHelper, points, direction, 0f, 1f, false);
            if (stitchEdgeWidth > 0f)
                AddOrganicEdge(vertexHelper);
        }

        void AddOriginReveal(VertexHelper vertexHelper, Vector2 direction)
        {
            GetProjectionRange(points, direction, out _, out float maximum);
            float origin = Vector2.Dot(revealOrigin, direction);
            float front = Mathf.Lerp(origin, maximum, revealProgress);

            // 填充前沿是垂直于移动方向的一条直线，前沿之后整格都算已绣。
            // 不再向反方向裁剪：否则进入边到原点之间会留下空缺，拐弯时表现为悬空的横带。
            ClipProjection(points, firstClip, direction, front, true);
            AddPolygon(vertexHelper, firstClip, direction, 0f, 1f, false);
        }

        void AddPolygon(
            VertexHelper vertexHelper,
            IReadOnlyList<Vector2> polygon,
            Vector2 direction,
            float innerThreshold,
            float outerThreshold,
            bool feathered)
        {
            if (polygon.Count < 3)
                return;

            triangles.Clear();
            EarClipTriangulator.Triangulate(polygon, triangles);
            int startIndex = vertexHelper.currentVertCount;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 point = polygon[i];
                UIVertex vertex = UIVertex.simpleVert;
                Color vertexColor = color;
                if (feathered)
                    vertexColor.a *= Mathf.InverseLerp(outerThreshold, innerThreshold, Vector2.Dot(point, direction));
                vertex.color = vertexColor;
                vertex.position = point;
                vertex.uv0 = point / textureTileSize;
                vertexHelper.AddVert(vertex);
            }

            for (int i = 0; i < triangles.Count; i += 3)
            {
                vertexHelper.AddTriangle(
                    startIndex + triangles[i],
                    startIndex + triangles[i + 1],
                    startIndex + triangles[i + 2]);
            }
        }

        void AddOrganicEdge(VertexHelper vertexHelper)
        {
            float signedArea = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[(i + 1) % points.Count];
                signedArea += start.x * end.y - end.x * start.y;
            }

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[(i + 1) % points.Count];
                Vector2 segment = end - start;
                if (segment.sqrMagnitude <= 0.0001f)
                    continue;
                Vector2 normal = signedArea >= 0f
                    ? new Vector2(segment.y, -segment.x).normalized
                    : new Vector2(-segment.y, segment.x).normalized;
                float startWidth = GetEdgeWidth(start);
                float endWidth = GetEdgeWidth(end);
                int index = vertexHelper.currentVertCount;
                AddVertex(vertexHelper, start, color);
                AddVertex(vertexHelper, end, color);
                AddVertex(vertexHelper, end + normal * endWidth, WithAlpha(color, 0f));
                AddVertex(vertexHelper, start + normal * startWidth, WithAlpha(color, 0f));
                vertexHelper.AddTriangle(index, index + 1, index + 2);
                vertexHelper.AddTriangle(index, index + 2, index + 3);
            }
        }

        float GetEdgeWidth(Vector2 point)
        {
            float noise = Mathf.Sin(point.x * 0.173f + point.y * 0.317f) * 0.5f + 0.5f;
            return Mathf.Max(0f, stitchEdgeWidth + (noise - 0.5f) * stitchEdgeNoise * 2f);
        }

        void AddVertex(VertexHelper vertexHelper, Vector2 point, Color vertexColor)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = vertexColor;
            vertex.position = point;
            vertex.uv0 = point / textureTileSize;
            vertexHelper.AddVert(vertex);
        }

        static void GetProjectionRange(
            IReadOnlyList<Vector2> polygon,
            Vector2 direction,
            out float minimum,
            out float maximum)
        {
            minimum = maximum = Vector2.Dot(polygon[0], direction);
            for (int i = 1; i < polygon.Count; i++)
            {
                float projection = Vector2.Dot(polygon[i], direction);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
        }

        static void ClipProjection(
            IReadOnlyList<Vector2> input,
            List<Vector2> output,
            Vector2 direction,
            float threshold,
            bool keepLess)
        {
            output.Clear();
            if (input.Count == 0)
                return;

            Vector2 previous = input[^1];
            float previousDistance = Vector2.Dot(previous, direction) - threshold;
            bool previousInside = keepLess ? previousDistance <= 0f : previousDistance >= 0f;
            for (int i = 0; i < input.Count; i++)
            {
                Vector2 current = input[i];
                float currentDistance = Vector2.Dot(current, direction) - threshold;
                bool currentInside = keepLess ? currentDistance <= 0f : currentDistance >= 0f;
                if (currentInside != previousInside)
                {
                    float t = previousDistance / (previousDistance - currentDistance);
                    output.Add(Vector2.Lerp(previous, current, t));
                }
                if (currentInside)
                    output.Add(current);
                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }
        }

        static Color WithAlpha(Color value, float alpha)
        {
            value.a = alpha;
            return value;
        }

        static class EarClipTriangulator
        {
            const float Epsilon = 0.00001f;

            public static void Triangulate(IReadOnlyList<Vector2> polygon, List<int> output)
            {
                output.Clear();
                int count = polygon.Count;
                if (count < 3)
                {
                    return;
                }

                var indices = new List<int>(count);
                if (SignedArea(polygon) > 0f)
                {
                    for (int i = 0; i < count; i++)
                    {
                        indices.Add(i);
                    }
                }
                else
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        indices.Add(i);
                    }
                }

                int guard = count * count;
                while (indices.Count > 2 && guard-- > 0)
                {
                    bool clipped = false;
                    for (int i = 0; i < indices.Count; i++)
                    {
                        int previous = indices[(i + indices.Count - 1) % indices.Count];
                        int current = indices[i];
                        int next = indices[(i + 1) % indices.Count];
                        if (!IsEar(polygon, indices, previous, current, next))
                        {
                            continue;
                        }

                        output.Add(previous);
                        output.Add(current);
                        output.Add(next);
                        indices.RemoveAt(i);
                        clipped = true;
                        break;
                    }

                    if (!clipped)
                    {
                        // 自交或重复点无法做耳切，退化为扇形，至少保证编辑器草稿可见。
                        output.Clear();
                        for (int i = 1; i < count - 1; i++)
                        {
                            output.Add(0);
                            output.Add(i);
                            output.Add(i + 1);
                        }

                        return;
                    }
                }
            }

            static bool IsEar(
                IReadOnlyList<Vector2> polygon,
                IReadOnlyList<int> indices,
                int previous,
                int current,
                int next)
            {
                Vector2 a = polygon[previous];
                Vector2 b = polygon[current];
                Vector2 c = polygon[next];
                if (Cross(b - a, c - b) <= Epsilon)
                {
                    return false;
                }

                for (int i = 0; i < indices.Count; i++)
                {
                    int index = indices[i];
                    if (index == previous || index == current || index == next)
                    {
                        continue;
                    }

                    if (PointInTriangle(polygon[index], a, b, c))
                    {
                        return false;
                    }
                }

                return true;
            }

            static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                float ab = Cross(b - a, p - a);
                float bc = Cross(c - b, p - b);
                float ca = Cross(a - c, p - c);
                return ab >= -Epsilon && bc >= -Epsilon && ca >= -Epsilon;
            }

            static float SignedArea(IReadOnlyList<Vector2> polygon)
            {
                float area = 0f;
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector2 a = polygon[i];
                    Vector2 b = polygon[(i + 1) % polygon.Count];
                    area += a.x * b.y - b.x * a.y;
                }

                return area * 0.5f;
            }

            static float Cross(Vector2 a, Vector2 b)
                => a.x * b.y - a.y * b.x;
        }
    }

    /// <summary>
    /// 蜿蜒网格的波形计算，编辑器和运行时共用，确保预览与实际棋盘一致。
    /// </summary>
    public static class DressMakingEmbroideryWavyGridUtility
    {
        public static float EvaluateOffset(
            float distance,
            int lineIndex,
            float wavelength,
            float amplitude,
            float phase,
            bool column)
        {
            if (amplitude <= 0f)
                return 0f;

            float safeWavelength = Mathf.Max(16f, wavelength);
            float linePhase = phase + lineIndex * 0.73f + (column ? 0.4f : 1.7f);
            float primary = Mathf.Sin(distance / safeWavelength * Mathf.PI * 2f + linePhase);
            float secondary = Mathf.Sin(distance / safeWavelength * Mathf.PI * 0.93f
                                       + linePhase * 1.61f);
            return amplitude * (primary * 0.68f + secondary * 0.32f);
        }

        public static bool IsDash(float distance, DressMakingEmbroideryWavyGridSettings settings)
        {
            if (settings == null || !settings.dashed)
                return true;

            float period = settings.DashLength + settings.GapLength;
            return Mathf.Repeat(distance, period) <= settings.DashLength;
        }
    }

    /// <summary>
    /// UGUI 蜿蜒网格层。它只绘制线条，raycastTarget 固定关闭，不会影响刺绣拖动输入。
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public abstract class DressMakingEmbroideryWavyGridGraphicBase : MaskableGraphic
    {
        Vector2 boardSize = new(1000f, 640f);
        DressMakingEmbroideryWavyGridSettings settings = new();

        public void SetGrid(Vector2 size, DressMakingEmbroideryWavyGridSettings value)
        {
            boardSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            settings = value ?? new DressMakingEmbroideryWavyGridSettings();
            settings.Normalize();
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (settings == null || !settings.enabled)
                return;

            Rect rect = ((RectTransform)transform).rect;
            float width = rect.width > 1f ? rect.width : boardSize.x;
            float height = rect.height > 1f ? rect.height : boardSize.y;
            if (width <= 1f || height <= 1f)
                return;

            float scaleX = width / boardSize.x;
            float scaleY = height / boardSize.y;
            int columns = Mathf.CeilToInt(boardSize.x / settings.ColumnSpacing);
            int rows = Mathf.CeilToInt(boardSize.y / settings.RowSpacing);
            int segments = settings.SegmentsPerLine;
            for (int column = 0; column <= columns; column++)
            {
                float x = column * settings.ColumnSpacing * scaleX;
                AddColumn(vertexHelper, rect, x, scaleX, scaleY, column, segments);
            }

            for (int row = 0; row <= rows; row++)
            {
                float y = row * settings.RowSpacing * scaleY;
                AddRow(vertexHelper, rect, y, scaleX, scaleY, row, segments);
            }
        }

        void AddColumn(
            VertexHelper vertexHelper,
            Rect rect,
            float x,
            float scaleX,
            float scaleY,
            int lineIndex,
            int segments)
        {
            float previousDistance = 0f;
            Vector2 previous = GetColumnPoint(rect, x, 0f, scaleX, scaleY, lineIndex);
            for (int i = 1; i <= segments; i++)
            {
                float normalized = i / (float)segments;
                float distance = normalized * boardSize.y;
                Vector2 current = GetColumnPoint(rect, x, distance, scaleX, scaleY, lineIndex);
                float segmentDistance = Vector2.Distance(previous, current);
                float midpoint = previousDistance + segmentDistance * 0.5f;
                if (DressMakingEmbroideryWavyGridUtility.IsDash(midpoint, settings))
                    AddLineSegment(vertexHelper, previous, current, settings.LineWidth, settings.color);
                previous = current;
                previousDistance += segmentDistance;
            }
        }

        void AddRow(
            VertexHelper vertexHelper,
            Rect rect,
            float y,
            float scaleX,
            float scaleY,
            int lineIndex,
            int segments)
        {
            float previousDistance = 0f;
            Vector2 previous = GetRowPoint(rect, y, 0f, scaleX, scaleY, lineIndex);
            for (int i = 1; i <= segments; i++)
            {
                float normalized = i / (float)segments;
                float distance = normalized * boardSize.x;
                Vector2 current = GetRowPoint(rect, y, distance, scaleX, scaleY, lineIndex);
                float segmentDistance = Vector2.Distance(previous, current);
                float midpoint = previousDistance + segmentDistance * 0.5f;
                if (DressMakingEmbroideryWavyGridUtility.IsDash(midpoint, settings))
                    AddLineSegment(vertexHelper, previous, current, settings.LineWidth, settings.color);
                previous = current;
                previousDistance += segmentDistance;
            }
        }

        Vector2 GetColumnPoint(
            Rect rect,
            float x,
            float distance,
            float scaleX,
            float scaleY,
            int lineIndex)
        {
            float offset = DressMakingEmbroideryWavyGridUtility.EvaluateOffset(
                distance,
                lineIndex,
                settings.ColumnWavelength,
                settings.ColumnAmplitude,
                settings.phase,
                true) * scaleX;
            float y = distance * scaleY;
            return new Vector2(rect.xMin + x + offset, rect.yMin + y);
        }

        Vector2 GetRowPoint(
            Rect rect,
            float y,
            float distance,
            float scaleX,
            float scaleY,
            int lineIndex)
        {
            float offset = DressMakingEmbroideryWavyGridUtility.EvaluateOffset(
                distance,
                lineIndex,
                settings.RowWavelength,
                settings.RowAmplitude,
                settings.phase + 0.37f,
                false) * scaleY;
            float x = distance * scaleX;
            return new Vector2(rect.xMin + x, rect.yMin + y + offset);
        }

        static void AddLineSegment(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float width,
            Color lineColor)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            int index = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = lineColor;
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
    }

    /// <summary>单个运行时刺绣块：底色 + 空间揭示填色层 + 数字标签。</summary>
    public abstract class DressMakingEmbroideryRegionViewBase : MonoBehaviour
    {
        [SerializeField] DressMakingEmbroideryPolygonGraphic baseGraphic;
        [SerializeField] DressMakingEmbroideryPolygonGraphic fillGraphic;
        [SerializeField] TextMeshProUGUI labelText;
        [SerializeField, Range(0f, 1f)] float unfilledAlpha = 0.12f;
        [SerializeField, Min(0f)] float fillDuration = 0.2f;

        List<Vector2> polygon = new();
        Color fillColor;
        Color completedColor;
        Vector2 previewOrigin;
        Vector2 revealDirection = Vector2.right;
        float fillProgress;
        float pulseTime = -1f;
        bool isFilled;
        bool isPreviewed;
        bool isAutoPreview;
        bool pulseAfterFill;

        public bool IsFilled => isFilled;
        public IReadOnlyList<Vector2> Polygon => polygon;
        public int RegionIndex { get; private set; }
        public int RequiredCount { get; private set; }
        public int Quantity { get; private set; }
        public bool IsNumberBlock { get; private set; }

        /// <summary>由关卡预制体生成器绑定绘制层。</summary>
        public void SetGraphics(
            DressMakingEmbroideryPolygonGraphic baseValue,
            DressMakingEmbroideryPolygonGraphic fillValue,
            TextMeshProUGUI labelValue)
        {
            baseGraphic = baseValue;
            fillGraphic = fillValue;
            labelText = labelValue;
        }

        public void SetData(
            int regionIndex,
            int requiredCount,
            int quantity,
            bool isNumberBlock,
            string label,
            Vector2 labelPosition,
            float labelFontSize,
            IReadOnlyList<Vector2> points,
            Color initialColor,
            Color resultColor,
            Texture stitchTexture,
            float stitchTileSize,
            float initialAlpha,
            float duration,
            float revealFeather,
            float edgeWidth,
            float edgeNoise)
        {
            RegionIndex = regionIndex;
            RequiredCount = requiredCount;
            Quantity = quantity;
            IsNumberBlock = isNumberBlock || requiredCount > 0;
            fillColor = stitchTexture != null ? Color.white : initialColor;
            completedColor = stitchTexture != null
                ? Color.white
                : resultColor.a > 0f ? resultColor : initialColor;
            unfilledAlpha = Mathf.Clamp01(initialAlpha);
            fillDuration = Mathf.Max(0f, duration);
            polygon = points == null ? new List<Vector2>() : new List<Vector2>(points);

            RectTransform rtf = (RectTransform)transform;
            rtf.anchoredPosition = Vector2.zero;
            rtf.localScale = Vector3.one;
            baseGraphic.SetPolygon(polygon);
            fillGraphic.SetPolygon(polygon);
            fillGraphic.SetTexture(stitchTexture, stitchTileSize);
            fillGraphic.SetStitchEffect(revealFeather, edgeWidth, edgeNoise);
            baseGraphic.color = WithAlpha(fillColor, unfilledAlpha);
            labelText.text = IsNumberBlock ? ResolveNumberText() : label;
            labelText.rectTransform.anchoredPosition = labelPosition;
            labelText.fontSize = Mathf.Max(8f, labelFontSize);
            ResetView();
        }

        public bool Contains(Vector2 localPoint)
            => EmbroideryGeometry.ContainsPoint(polygon, localPoint);

        public void ResetView()
        {
            isFilled = false;
            isPreviewed = false;
            isAutoPreview = false;
            pulseAfterFill = false;
            pulseTime = -1f;
            fillProgress = 0f;
            revealDirection = Vector2.right;
            fillGraphic.ClearRevealOrigin();
            baseGraphic.color = WithAlpha(fillColor, unfilledAlpha);
            fillGraphic.color = completedColor;
            fillGraphic.canvasRenderer.SetAlpha(1f);
            fillGraphic.SetReveal(0f, revealDirection);
            labelText.rectTransform.localScale = Vector3.one;
            labelText.gameObject.SetActive(IsNumberBlock || !string.IsNullOrEmpty(labelText.text));
        }

        public void StartPreview(Vector2 pointerPosition, Vector2 direction, bool autoFill)
        {
            if (isFilled)
                return;
            isPreviewed = true;
            isAutoPreview = autoFill;
            previewOrigin = pointerPosition;
            fillProgress = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                revealDirection = direction.normalized;
            fillGraphic.color = completedColor;
            fillGraphic.SetRevealOrigin(pointerPosition);
            fillGraphic.SetReveal(0f, revealDirection);
        }
        public void UpdatePreview(Vector2 pointerPosition, Vector2 direction)
        {
            if (!isPreviewed || isFilled || fillProgress >= 1f)
                return;
            Vector2 movement = pointerPosition - previewOrigin;
            if (movement.sqrMagnitude <= 1f)
                return;

            // 揭示方向按"本格进入点 → 当前指针"实时计算，拐弯后立刻改成新方向；
            // 位移不足时沿用外部传入的拖拽方向，避免抖动。
            if (movement.sqrMagnitude > 4f)
                revealDirection = movement.normalized;
            else if (direction.sqrMagnitude > 0.0001f)
                revealDirection = direction.normalized;

            float origin = Vector2.Dot(previewOrigin, revealDirection);
            float span = Mathf.Max(8f, GetMaxProjection(revealDirection) - origin);
            float advance = Vector2.Dot(movement, revealDirection);
            fillProgress = Mathf.Max(fillProgress, Mathf.Clamp01(advance / span));
            fillGraphic.SetReveal(fillProgress, revealDirection);
        }

        float GetMaxProjection(Vector2 direction)
        {
            float maximum = float.NegativeInfinity;
            for (int i = 0; i < polygon.Count; i++)
                maximum = Mathf.Max(maximum, Vector2.Dot(polygon[i], direction));
            return maximum;
        }

        public void CompletePreview()
        {
            if (!isPreviewed || isFilled)
                return;
            fillProgress = 1f;
            fillGraphic.SetReveal(1f, revealDirection);
        }

        public void SetPreviewed(bool value)
        {
            if (isFilled || isPreviewed == value)
                return;
            if (value)
            {
                StartPreview(EmbroideryGeometry.GetBounds(polygon).center, revealDirection, false);
                return;
            }
            isPreviewed = false;
            isAutoPreview = false;
            fillProgress = 0f;
            fillGraphic.SetReveal(0f, revealDirection);
        }

        public void CompleteFill(bool pulseNumber)
        {
            isFilled = true;
            isPreviewed = false;
            isAutoPreview = false;
            pulseAfterFill = pulseNumber && IsNumberBlock;
            if (!pulseAfterFill)
                labelText.gameObject.SetActive(false);
            if (fillDuration <= 0f || fillProgress >= 1f)
            {
                fillProgress = 1f;
                fillGraphic.SetReveal(1f, revealDirection);
                StartPendingPulse();
            }
        }

        public void TickFill(float deltaTime)
        {
            if ((isFilled || isPreviewed && isAutoPreview) && fillProgress < 1f)
            {
                fillProgress = fillDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(fillProgress + deltaTime / fillDuration);
                fillGraphic.SetReveal(fillProgress, revealDirection);
                if (fillProgress >= 1f)
                    StartPendingPulse();
            }

            if (pulseTime < 0f)
                return;
            pulseTime += deltaTime;
            float normalized = Mathf.Clamp01(pulseTime / 0.36f);
            labelText.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(normalized * Mathf.PI) * 0.32f);
            if (normalized < 1f)
                return;
            pulseTime = -1f;
            labelText.rectTransform.localScale = Vector3.one;
        }

        public void SetHighlighted(bool highlighted)
        {
            baseGraphic.color = highlighted
                ? WithAlpha(Color.Lerp(fillColor, Color.white, 0.25f), Mathf.Clamp01(unfilledAlpha + 0.12f))
                : WithAlpha(fillColor, unfilledAlpha);
        }

        void StartPendingPulse()
        {
            if (!pulseAfterFill)
                return;
            pulseAfterFill = false;
            pulseTime = 0f;
            labelText.gameObject.SetActive(true);
        }

        string ResolveNumberText()
            => Mathf.Max(1, RequiredCount > 0 ? RequiredCount : Quantity).ToString();

        static Color WithAlpha(Color value, float alpha)
        {
            value.a = alpha;
            return value;
        }
    }

    /// <summary>
    /// 运行时刺绣棋盘。输入层只接收 Pointer 事件，区域命中由多边形计算完成，兼容鼠标和 iOS 触摸。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DressMakingEmbroiderySimulationGameBoard : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler,
        IPointerMoveHandler
    {
        [Header("引用")]
        [SerializeField] RectTransform boardRoot;
        [SerializeField] Transform regionContainer;
        [SerializeField] DressMakingEmbroideryRegionView regionPrefab;
        [SerializeField] DressMakingEmbroideryWavyGridGraphic wavyGridGraphic;
        [SerializeField] DressMakingEmbroideryGridDividerGraphic gridDividerGraphic;
        [SerializeField] RectTransform inputSurface;
        [SerializeField] Graphic inputGraphic;
        [SerializeField] RectTransform counterRoot;
        [SerializeField] TextMeshProUGUI counterText;
        [SerializeField] Color inputSurfaceColor = Color.clear;

        [Header("显示")]
        [SerializeField] float regionUnfilledAlpha = 0.12f;
        [SerializeField] float regionFillDuration = 0.2f;
        [SerializeField, Min(0.5f)] float fillRevealFeather = 10f;
        [SerializeField, Min(0f)] float stitchEdgeWidth = 3f;
        [SerializeField, Min(0f)] float stitchEdgeNoise = 1.5f;
        [SerializeField] Vector2 counterOffset = new(0f, 42f);
        [SerializeField] Texture2D defaultStitchTexture;
        [SerializeField, Min(4f)] float defaultStitchTileSize = 32f;

        [Header("规则")]
        [SerializeField] bool includeNumberBlockInCount = true;
        [SerializeField] bool countByQuantity;
        [SerializeField, Min(0f)] float automaticAdjacencyTolerance = 8f;
        [SerializeField, Min(0f)] float automaticAdjacencyMinimumLength = 4f;
        [SerializeField] bool allowRestartOnInvalidRelease = true;

        readonly List<DressMakingEmbroideryRegionView> regionViews = new();
        readonly List<EmbroideryPathRegion> regionSnapshots = new();
        readonly List<int> activePath = new();
        readonly List<List<int>> completedPaths = new();

        DressMakingEmbroideryLevelData levelData;
        Action<bool> completedCallback;
        Camera uiCamera;
        int activeNumberIndex = -1;
        int activeRequiredCount;
        Vector2 lastPointerLocal;
        Vector2 pathStartLocal;
        Vector2 pathDirection = Vector2.right;
        int activePointerId = int.MinValue;
        float samplingStep = 16f;
        float completionDelay = -1f;
        string pathError;
        bool isPointerDown;
        bool isFinished;

        static Texture2D generatedStitchTexture;

        public IReadOnlyList<int> ActivePath => activePath;
        public IReadOnlyList<DressMakingEmbroideryRegionView> RegionViews => regionViews;
        public bool IsFinished => isFinished;
        public EmbroideryPathValidation LastValidation { get; private set; }

        public void SetReferences(
            RectTransform root,
            Transform container,
            DressMakingEmbroideryRegionView prefab,
            DressMakingEmbroideryWavyGridGraphic gridGraphic,
            DressMakingEmbroideryGridDividerGraphic dividerGraphic,
            RectTransform surface,
            Graphic surfaceGraphic,
            RectTransform countRoot,
            TextMeshProUGUI countText)
        {
            boardRoot = root;
            regionContainer = container;
            regionPrefab = prefab;
            wavyGridGraphic = gridGraphic;
            gridDividerGraphic = dividerGraphic;
            inputSurface = surface;
            inputGraphic = surfaceGraphic;
            counterRoot = countRoot;
            counterText = countText;
        }

        public bool StartGame(DressMakingEmbroideryLevelData data, Action<bool> onCompleted = null, Camera camera = null)
        {
            levelData = data;
            completedCallback = onCompleted;
            uiCamera = camera;
            isFinished = false;
            completionDelay = -1f;
            LastValidation = EmbroideryPathValidation.Invalid("not-started");
            StopPath();
            ClearViews();

            if (levelData == null || levelData.Regions == null || levelData.Regions.Count == 0)
            {
                Debug.LogError("[Embroidery] 关卡没有可用区域配置。");
                return false;
            }

            for (int i = 0; i < levelData.Regions.Count; i++)
            {
                DressMakingEmbroideryRegionData region = levelData.Regions[i];
                if (region == null || region.GetSampledBoundary().Count < 3)
                {
                    Debug.LogError($"[Embroidery] 区域 {i} 为空或边界点不足。");
                    return false;
                }
            }

            includeNumberBlockInCount = levelData.IncludeNumberBlockInCount;
            countByQuantity = levelData.CountByQuantity;
            SetBoardSize(levelData.CanvasSize);
            CreateViews();
            CreateWavyGrid();
            CreateGridDivider();
            CreateInputSurface();
            samplingStep = CalculateSamplingStep();
            return true;
        }

        public void StopGame()
        {
            StopPath();
            completionDelay = -1f;
            completedCallback = null;
            isFinished = true;
        }

        public void ResetGame()
        {
            if (levelData == null)
            {
                return;
            }

            isFinished = false;
            completionDelay = -1f;
            StopPath();
            completedPaths.Clear();
            for (int i = 0; i < regionViews.Count; i++)
            {
                regionViews[i].ResetView();
            }

            RefreshSnapshots();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (levelData == null
                || isPointerDown
                || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            uiCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : uiCamera;
            if (!GetBoardLocalPoint(eventData.position, out Vector2 localPoint))
            {
                return;
            }

            int selectedRegionIndex = FindRegion(localPoint);
            if (selectedRegionIndex >= 0 && regionViews[selectedRegionIndex].IsFilled)
            {
                UndoCompletedPath(selectedRegionIndex);
                return;
            }
            if (isFinished)
                return;

            StopPath();
            isPointerDown = true;
            activePointerId = eventData.pointerId;
            lastPointerLocal = localPoint;
            pathStartLocal = localPoint;
            AddRegionAt(localPoint);
            UpdateCounter(localPoint);
            if (activePath.Count == 0)
                StopPath();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isPointerDown || isFinished || eventData.pointerId != activePointerId)
            {
                return;
            }

            if (GetBoardLocalPoint(eventData.position, out Vector2 localPoint))
            {
                // 方向实时跟随当前拖拽段，新进入的格子按进入方向起绣，已绣满的格子不受影响。
                Vector2 segmentMovement = localPoint - lastPointerLocal;
                if (segmentMovement.sqrMagnitude > 1f)
                    pathDirection = segmentMovement.normalized;
                SampleSegment(lastPointerLocal, localPoint);
                if (activePath.Count > 0)
                    regionViews[activePath[^1]].UpdatePreview(localPoint, pathDirection);
                lastPointerLocal = localPoint;
                UpdateCounter(localPoint);
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (isPointerDown && eventData.pointerId == activePointerId)
            {
                OnDrag(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isPointerDown || eventData.pointerId != activePointerId)
            {
                return;
            }

            isPointerDown = false;
            if (activePath.Count == 0)
            {
                StopPath();
                return;
            }

            if (!string.IsNullOrEmpty(pathError))
            {
                LastValidation = EmbroideryPathValidation.Invalid(pathError);
                HandleInvalidPath();
                return;
            }

            RefreshSnapshots();
            EmbroideryPathValidation validation = EmbroideryPathRuleEngine.ValidatePath(
                regionSnapshots,
                activePath,
                includeNumberBlockInCount,
                countByQuantity);
            LastValidation = validation;

            if (!validation.IsValid)
            {
                HandleInvalidPath();
                return;
            }

            CompletePath();
        }

        void SetBoardSize(Vector2 size)
        {
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            boardRoot.sizeDelta = size;
        }

        void CreateViews()
        {
            for (int i = 0; i < levelData.Regions.Count; i++)
            {
                DressMakingEmbroideryRegionData data = levelData.Regions[i];
                DressMakingEmbroideryRegionView view = CreateRegionView();
                List<Vector2> polygon = GetRegionPolygon(data);
                view.SetData(
                    i,
                    data.requiredCount,
                    data.quantity,
                    data.isNumberBlock,
                    data.label,
                    GetLabelPosition(data.labelPosition),
                    data.LabelFontSize,
                    polygon,
                    data.fillColor,
                    data.completedColor,
                    data.FillTexture != null ? data.FillTexture : GetStitchTexture(),
                    data.stitchTileSize > 0f ? data.StitchTileSize : defaultStitchTileSize,
                    regionUnfilledAlpha,
                    regionFillDuration,
                    fillRevealFeather,
                    stitchEdgeWidth,
                    stitchEdgeNoise);
                view.name = $"Region_{data.id}";
                regionViews.Add(view);
            }

            RefreshSnapshots();
        }

        void CreateWavyGrid()
        {
            DressMakingEmbroideryWavyGridSettings gridSettings = levelData.WavyGrid;
            wavyGridGraphic.SetGrid(levelData.CanvasSize, gridSettings);
            if (gridSettings.overlay)
                wavyGridGraphic.transform.SetAsLastSibling();
            else
                wavyGridGraphic.transform.SetSiblingIndex(0);
            wavyGridGraphic.gameObject.SetActive(gridSettings.enabled && !levelData.GridDivider.enabled);
        }

        void CreateGridDivider()
        {
            DressMakingEmbroideryGridDividerSettings settings = levelData.GridDivider;
            gridDividerGraphic.SetCells(regionViews, settings);
            gridDividerGraphic.transform.SetAsLastSibling();
            gridDividerGraphic.gameObject.SetActive(settings.enabled);
        }

        DressMakingEmbroideryRegionView CreateRegionView()
        {
            DressMakingEmbroideryRegionView view = Instantiate(regionPrefab, regionContainer);
            view.gameObject.SetActive(true);
            return view;
        }

        void CreateInputSurface()
        {
            inputSurface.SetAsLastSibling();
            inputGraphic.color = inputSurfaceColor;
            inputGraphic.raycastTarget = true;
        }

        void ClearViews()
        {
            for (int i = regionViews.Count - 1; i >= 0; i--)
            {
                if (regionViews[i] != null)
                {
                    Destroy(regionViews[i].gameObject);
                }
            }

            regionViews.Clear();
            regionSnapshots.Clear();
            completedPaths.Clear();
        }

        void RefreshSnapshots()
        {
            regionSnapshots.Clear();
            for (int i = 0; i < regionViews.Count; i++)
            {
                DressMakingEmbroideryRegionView view = regionViews[i];
                regionSnapshots.Add(new EmbroideryPathRegion(
                    view.RequiredCount,
                    view.Quantity,
                    view.IsNumberBlock,
                    view.IsFilled));
            }
        }

        List<Vector2> GetRegionPolygon(DressMakingEmbroideryRegionData data)
        {
            List<Vector2> points = data.GetSampledBoundary();
            if (points.Count < 3)
                return new List<Vector2>();

            var result = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                result.Add(ToBoardPoint(points[i]));
            }

            return result;
        }

        Vector2 GetLabelPosition(Vector2 normalizedPosition) => ToBoardPoint(normalizedPosition);

        Vector2 ToBoardPoint(Vector2 normalizedPoint)
        {
            Rect rect = boardRoot.rect;
            return new Vector2(
                rect.xMin + normalizedPoint.x * rect.width,
                rect.yMin + normalizedPoint.y * rect.height);
        }

        bool GetBoardLocalPoint(Vector2 screenPoint, out Vector2 localPoint)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boardRoot,
                screenPoint,
                uiCamera,
                out localPoint);
        }

        void SampleSegment(Vector2 start, Vector2 end)
        {
            int sampleCount = Mathf.Max(
                1,
                Mathf.CeilToInt(Vector2.Distance(start, end) / samplingStep));
            for (int i = 1; i <= sampleCount; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / (float)sampleCount);
                AddRegionAt(point);
                if (!isPointerDown)
                    break;
            }
        }

        float CalculateSamplingStep()
        {
            if (regionViews.Count == 0)
            {
                return 16f;
            }

            Rect bounds = EmbroideryGeometry.GetBounds(regionViews[0].Polygon);
            float smallest = Mathf.Min(bounds.width, bounds.height);
            for (int i = 1; i < regionViews.Count; i++)
            {
                bounds = EmbroideryGeometry.GetBounds(regionViews[i].Polygon);
                smallest = Mathf.Min(smallest, Mathf.Min(bounds.width, bounds.height));
            }

            return Mathf.Max(4f, smallest * 0.25f);
        }

        void AddRegionAt(Vector2 localPoint)
        {
            if (!string.IsNullOrEmpty(pathError))
                return;

            int regionIndex = FindRegion(localPoint);
            if (regionIndex < 0)
            {
                if (activePath.Count > 0
                    && !IsNearPolygon(localPoint, regionViews[activePath[^1]].Polygon, automaticAdjacencyTolerance))
                    SetPathError("path-broken");
                return;
            }

            if (activePath.Count > 0 && activePath[^1] == regionIndex)
                return;
            if (activePath.Contains(regionIndex))
                return;

            if (activeRequiredCount > 0 && GetActiveTraversalCount() >= activeRequiredCount)
            {
                // 达到数字要求后，继续拖到下一个网格只视为越过目标，
                // 不把该网格加入路径，也不应因此使本次刺绣失败。
                return;
            }

            DressMakingEmbroideryRegionView view = regionViews[regionIndex];
            if (view.IsFilled)
            {
                SetPathError("region-filled");
                return;
            }

            if (view.IsNumberBlock && activeNumberIndex >= 0)
            {
                SetPathError("multiple-number-blocks");
                return;
            }

            if (activePath.Count > 0 && !CanConnect(activePath[^1], regionIndex))
            {
                SetPathError("regions-not-adjacent");
                return;
            }

            if (activePath.Count > 0)
                regionViews[activePath[^1]].CompletePreview();

            bool isFirstRegion = activePath.Count == 0;
            activePath.Add(regionIndex);
            view.SetHighlighted(true);
            view.StartPreview(
                isFirstRegion ? pathStartLocal : localPoint,
                pathDirection,
                isFirstRegion);
            if (view.IsNumberBlock)
            {
                activeNumberIndex = regionIndex;
                activeRequiredCount = Mathf.Max(1, view.RequiredCount > 0 ? view.RequiredCount : view.Quantity);
                if (GetActiveTraversalCount() > activeRequiredCount)
                    SetPathError("count-exceeded");
            }
        }

        void UpdateCounter(Vector2 localPoint)
        {
            if (counterRoot == null || counterText == null)
                return;
            bool visible = isPointerDown && activePath.Count > 0;
            counterRoot.gameObject.SetActive(visible);
            if (!visible)
                return;
            counterRoot.anchoredPosition = localPoint + counterOffset;
            counterRoot.SetAsLastSibling();
            counterText.text = Mathf.Max(1, GetActiveTraversalCount()).ToString();
        }

        static bool IsNearPolygon(Vector2 point, IReadOnlyList<Vector2> polygon, float tolerance)
        {
            if (polygon == null || polygon.Count < 2)
                return false;

            for (int i = 0; i < polygon.Count; i++)
            {
                if (EmbroideryGeometry.DistanceToSegment(
                        point,
                        polygon[i],
                        polygon[(i + 1) % polygon.Count]) <= tolerance)
                    return true;
            }

            return false;
        }

        void SetPathError(string error)
        {
            if (string.IsNullOrEmpty(pathError))
                pathError = error;
        }

        bool CanConnect(int fromIndex, int toIndex)
        {
            DressMakingEmbroideryRegionData from = levelData.Regions[fromIndex];
            DressMakingEmbroideryRegionData to = levelData.Regions[toIndex];
            bool hasExplicitNeighbours = from.neighbourIds is { Count: > 0 }
                                         || to.neighbourIds is { Count: > 0 };
            if (hasExplicitNeighbours)
            {
                return from.neighbourIds?.Contains(to.id) == true
                    || to.neighbourIds?.Contains(from.id) == true;
            }

            return EmbroideryGeometry.ArePolygonsAdjacent(
                regionViews[fromIndex].Polygon,
                regionViews[toIndex].Polygon,
                automaticAdjacencyTolerance,
                automaticAdjacencyMinimumLength);
        }

        int GetActiveTraversalCount()
        {
            int count = 0;
            for (int i = 0; i < activePath.Count; i++)
            {
                int regionIndex = activePath[i];
                if (!includeNumberBlockInCount && regionIndex == activeNumberIndex)
                    continue;
                count += countByQuantity ? Mathf.Max(1, regionViews[regionIndex].Quantity) : 1;
            }

            return count;
        }

        int FindRegion(Vector2 localPoint)
        {
            // 后创建的区域在视觉上位于上层，优先命中它。
            for (int i = regionViews.Count - 1; i >= 0; i--)
            {
                if (regionViews[i].Contains(localPoint))
                {
                    return i;
                }
            }

            return -1;
        }

        void CompletePath()
        {
            completedPaths.Add(new List<int>(activePath));
            for (int i = 0; i < activePath.Count; i++)
            {
                int regionIndex = activePath[i];
                regionViews[regionIndex].CompleteFill(regionIndex == activeNumberIndex);
            }

            StopPath();
            RefreshSnapshots();
            bool allFilled = true;
            for (int i = 0; i < regionViews.Count; i++)
            {
                if (regionViews[i].IsFilled)
                    continue;
                allFilled = false;
                break;
            }

            if (allFilled)
            {
                isFinished = true;
                completionDelay = Mathf.Max(0f, regionFillDuration) + 0.42f;
            }
        }

        void UndoCompletedPath(int regionIndex)
        {
            for (int i = completedPaths.Count - 1; i >= 0; i--)
            {
                List<int> path = completedPaths[i];
                if (!path.Contains(regionIndex))
                    continue;
                for (int j = 0; j < path.Count; j++)
                    regionViews[path[j]].ResetView();
                completedPaths.RemoveAt(i);
                completionDelay = -1f;
                isFinished = false;
                LastValidation = EmbroideryPathValidation.Invalid("path-undone");
                RefreshSnapshots();
                return;
            }
        }

        void HandleInvalidPath()
        {
            for (int i = 0; i < activePath.Count; i++)
            {
                regionViews[activePath[i]].SetPreviewed(false);
                regionViews[activePath[i]].SetHighlighted(false);
            }

            if (allowRestartOnInvalidRelease)
                StopPath();
        }

        void StopPath()
        {
            isPointerDown = false;
            activePointerId = int.MinValue;
            activeNumberIndex = -1;
            activeRequiredCount = 0;
            pathDirection = Vector2.right;
            pathError = null;
            if (counterRoot != null)
                counterRoot.gameObject.SetActive(false);
            for (int i = 0; i < activePath.Count; i++)
            {
                if (activePath[i] >= 0 && activePath[i] < regionViews.Count)
                    regionViews[activePath[i]].SetHighlighted(false);
            }

            activePath.Clear();
        }

        Texture2D GetStitchTexture()
        {
            if (defaultStitchTexture != null)
                return defaultStitchTexture;
            if (generatedStitchTexture != null)
                return generatedStitchTexture;

            const int size = 32;
            generatedStitchTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "GeneratedEmbroideryStitch",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float phase = Mathf.Repeat(x + y * 0.82f, 16f);
                    float distance = Mathf.Abs(phase - 8f);
                    float strand = 1f - Mathf.SmoothStep(0f, 7f, distance);
                    float twist = 0.85f + 0.15f * Mathf.Sin((x - y) * 0.7f);
                    byte value = (byte)Mathf.RoundToInt(
                        Mathf.Lerp(105f, 245f, strand * twist));
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            }

            generatedStitchTexture.SetPixels32(pixels);
            generatedStitchTexture.Apply(false, true);
            return generatedStitchTexture;
        }

        void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = 0; i < regionViews.Count; i++)
                regionViews[i].TickFill(deltaTime);
            if (completionDelay < 0f)
                return;
            completionDelay -= deltaTime;
            if (completionDelay > 0f)
                return;
            completionDelay = -1f;
            completedCallback?.Invoke(true);
        }

        void OnDisable()
        {
            StopPath();
        }
    }
}
