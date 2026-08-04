using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>
    /// 取 Image 上 Sprite 的实际图形范围(不含四周透明留白)。
    /// 素材图常常是一张大画布中间只有一小块内容,直接用 RectTransform 判断落点,
    /// 会把透明的空白区域也算成命中,所以拖拽落点判定要基于实际图形。
    /// </summary>
    public static class UISpriteShapeUtils
    {
        private static readonly List<Vector2> PhysicsShapeBuffer = new List<Vector2>();

        /// <summary>
        /// 取实际图形轮廓,并转换到 target 的本地坐标系。
        /// 优先用 Sprite 的物理形状,其次用 Tight 网格顶点(导入时按 alpha 自动生成),
        /// 两者都没有时回退为 RectTransform 的四角。
        /// </summary>
        public static List<Vector2[]> GetShapePolygons(Image image, RectTransform rect, RectTransform target)
        {
            var polygons = new List<Vector2[]>();
            if (rect == null || target == null)
            {
                return polygons;
            }

            var sprite = image != null ? image.sprite : null;
            if (sprite != null)
            {
                for (var i = 0; i < sprite.GetPhysicsShapeCount(); i++)
                {
                    PhysicsShapeBuffer.Clear();
                    sprite.GetPhysicsShape(i, PhysicsShapeBuffer);
                    if (PhysicsShapeBuffer.Count < 3)
                    {
                        continue;
                    }

                    var points = new Vector2[PhysicsShapeBuffer.Count];
                    for (var j = 0; j < PhysicsShapeBuffer.Count; j++)
                    {
                        points[j] = SpritePointToTargetLocal(sprite, rect, PhysicsShapeBuffer[j], target);
                    }

                    polygons.Add(points);
                }

                if (polygons.Count == 0)
                {
                    var vertices = sprite.vertices;
                    if (vertices != null && vertices.Length >= 3)
                    {
                        var points = new Vector2[vertices.Length];
                        for (var i = 0; i < vertices.Length; i++)
                        {
                            points[i] = SpritePointToTargetLocal(sprite, rect, vertices[i], target);
                        }

                        polygons.Add(points);
                    }
                }
            }

            if (polygons.Count == 0)
            {
                polygons.Add(GetRectCorners(rect, target));
            }

            return polygons;
        }

        /// <summary>
        /// 取实际图形的包围盒(target 本地坐标系)。没有可用图形时返回 false。
        /// </summary>
        public static bool TryGetContentBounds(Image image, RectTransform rect, RectTransform target, out Rect bounds)
        {
            bounds = default;
            var polygons = GetShapePolygons(image, rect, target);
            var hasPoint = false;
            var min = Vector2.zero;
            var max = Vector2.zero;
            foreach (var polygon in polygons)
            {
                foreach (var point in polygon)
                {
                    if (!hasPoint)
                    {
                        min = point;
                        max = point;
                        hasPoint = true;
                        continue;
                    }

                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }

            if (!hasPoint)
            {
                return false;
            }

            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        /// <summary>
        /// Sprite 自己的 pivot 落在 rect 本地坐标的哪里。
        /// 素材是整张人物画布大小、配件只占其中一小块,美术在 Sprite Editor 里把 pivot
        /// 打在配件上，这个点就是"这个配件的中心点"，用来定位和判定落点。
        /// 拿不到 Sprite 时返回 false。
        /// </summary>
        public static bool TryGetSpritePivotLocal(Image image, RectTransform rect, out Vector2 pivotLocal)
        {
            pivotLocal = default;
            var sprite = image != null ? image.sprite : null;
            if (sprite == null || rect == null)
            {
                return false;
            }

            // Sprite 的顶点坐标都以 pivot 为原点,所以把 (0,0) 换算过去就是 pivot 的位置
            pivotLocal = SpritePointToRectLocal(sprite, rect, Vector2.zero);
            return true;
        }

        /// <summary>
        /// 取实际图形的三角面(rect 本地坐标),用于点击命中判定。
        /// Tight 网格导入时已经按 alpha 剔掉了四周的透明留白,拿它判定就不会点到空白处。
        /// 返回长度是 3 的倍数,每三个点一个三角面;拿不到网格时返回 null。
        /// 顶点位置只跟 Sprite 和 rect 尺寸有关,与 rect 当前位置无关,所以结果可以缓存复用。
        /// </summary>
        public static Vector2[] GetShapeTrianglesLocal(Image image, RectTransform rect)
        {
            var sprite = image != null ? image.sprite : null;
            if (sprite == null || rect == null)
            {
                return null;
            }

            var vertices = sprite.vertices;
            var triangles = sprite.triangles;
            if (vertices == null || triangles == null || vertices.Length < 3 || triangles.Length < 3)
            {
                return null;
            }

            var localVertices = new Vector2[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                localVertices[i] = SpritePointToRectLocal(sprite, rect, vertices[i]);
            }

            var shapeTriangles = new Vector2[triangles.Length];
            for (var i = 0; i < triangles.Length; i++)
            {
                shapeTriangles[i] = localVertices[triangles[i]];
            }

            return shapeTriangles;
        }

        /// <summary>
        /// 落点是否落在 GetShapeTrianglesLocal 返回的图形内。
        /// </summary>
        public static bool IsPointInsideTriangles(Vector2[] shapeTriangles, Vector2 point)
        {
            if (shapeTriangles == null)
            {
                return false;
            }

            for (var i = 0; i + 2 < shapeTriangles.Length; i += 3)
            {
                if (IsPointInTriangle(point, shapeTriangles[i], shapeTriangles[i + 1], shapeTriangles[i + 2]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Cross(b - a, point - a);
            var d2 = Cross(c - b, point - b);
            var d3 = Cross(a - c, point - c);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;

            // 三条边的叉积同号(允许 0)才说明点在三角形内,不用管顶点绕向
            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        /// <summary>
        /// Sprite 的顶点 / 物理形状是「相对 pivot 的单位坐标」,换算成 rect 本地坐标。
        /// 注意不能拿 sprite.bounds 来归一化: Tight 网格的 bounds 只包住实际图形,
        /// 用它当分母会把一小块内容拉伸成整个 Rect,算出来的图形范围和中心全是错的。
        /// 要用 sprite.rect(整张含留白的尺寸)配 sprite.pivot 换算,这样 FullRect 和 Tight 都对。
        /// </summary>
        private static Vector2 SpritePointToRectLocal(Sprite sprite, RectTransform rect, Vector2 spritePoint)
        {
            var rectArea = rect.rect;
            var spriteRect = sprite.rect;
            var pixelPoint = spritePoint * sprite.pixelsPerUnit + sprite.pivot;

            var normalizedX = spriteRect.width > 0f ? pixelPoint.x / spriteRect.width : 0.5f;
            var normalizedY = spriteRect.height > 0f ? pixelPoint.y / spriteRect.height : 0.5f;

            return new Vector2(
                Mathf.Lerp(rectArea.xMin, rectArea.xMax, normalizedX),
                Mathf.Lerp(rectArea.yMin, rectArea.yMax, normalizedY)
            );
        }

        private static Vector2 SpritePointToTargetLocal(Sprite sprite, RectTransform rect, Vector2 spritePoint, RectTransform target)
        {
            return target.InverseTransformPoint(rect.TransformPoint(SpritePointToRectLocal(sprite, rect, spritePoint)));
        }

        private static Vector2[] GetRectCorners(RectTransform rect, RectTransform target)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var points = new Vector2[corners.Length];
            for (var i = 0; i < corners.Length; i++)
            {
                points[i] = target.InverseTransformPoint(corners[i]);
            }

            return points;
        }
    }
}
