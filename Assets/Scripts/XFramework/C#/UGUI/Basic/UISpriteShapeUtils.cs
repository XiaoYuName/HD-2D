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

        private static Vector2 SpritePointToTargetLocal(Sprite sprite, RectTransform rect, Vector2 spritePoint, RectTransform target)
        {
            var bounds = sprite.bounds;
            var rectArea = rect.rect;

            var normalizedX = Mathf.InverseLerp(bounds.min.x, bounds.max.x, spritePoint.x);
            var normalizedY = Mathf.InverseLerp(bounds.min.y, bounds.max.y, spritePoint.y);
            var rectLocalPoint = new Vector2(
                Mathf.Lerp(rectArea.xMin, rectArea.xMax, normalizedX),
                Mathf.Lerp(rectArea.yMin, rectArea.yMax, normalizedY)
            );

            return target.InverseTransformPoint(rect.TransformPoint(rectLocalPoint));
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
