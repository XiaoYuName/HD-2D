using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>拖动时显示路径计数的圆形底板。</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class DressMakingEmbroideryCounterGraphic : MaskableGraphic
    {
        [SerializeField, Range(12, 64)] int segments = 32;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = ((RectTransform)transform).rect;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vertex.uv0 = new Vector2(0.5f, 0.5f);
            vertexHelper.AddVert(vertex);

            int count = Mathf.Clamp(segments, 12, 64);
            for (int i = 0; i <= count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                vertex.position = center + direction * radius;
                vertex.uv0 = direction * 0.5f + Vector2.one * 0.5f;
                vertexHelper.AddVert(vertex);
                if (i > 0)
                    vertexHelper.AddTriangle(0, i, i + 1);
            }
        }
    }
}
