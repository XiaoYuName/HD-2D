using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Utilities2D;

/// <summary>
/// 目标切割路径：定义玩家必须沿着切的那条虚线。
/// 数据来源默认直接读取同物体上的 EdgeCollider2D，视觉表现（虚线贴图）与判定数据因此天然分离。
/// </summary>
public class GemCutTarget : MonoBehaviour
{
    /// <summary>一条待切的目标边（世界坐标）。</summary>
    public struct Edge
    {
        public int index;
        public Vector2 p;
        public Vector2 q;

        public Vector2 Center => (p + q) * 0.5f;
        public float Length => Vector2.Distance(p, q);
    }

    [Title("路径来源")]
    [LabelText("自动读取 EdgeCollider2D")]
    [Tooltip("勾上则用同物体的 EdgeCollider2D 顶点作为切割路径；关掉则用下面手填的点。")]
    public bool useEdgeCollider = true;

    [LabelText("手填路径点（局部坐标）")]
    [ShowIf("@!this.useEdgeCollider")]
    public List<Vector2> localPoints = new List<Vector2>();

    [LabelText("闭合路径")]
    [Tooltip("勾上会额外生成一条『最后一点 → 第一点』的边。路径本身已经首尾相接时不要勾。")]
    public bool closed = false;

    [Title("保留区域")]
    [LabelText("自动取路径中心为保留点")]
    public bool autoKeepPoint = true;

    [LabelText("保留点（局部坐标）")]
    [ShowIf("@!this.autoKeepPoint")]
    [Tooltip("切完之后要留下的那一侧。碎块里包含这个点的那块就是宝石本体，其余全部当废料丢掉。")]
    public Vector2 keepPointLocal;

    [Title("Gizmos")]
    [LabelText("绘制路径")] public bool drawGizmos = true;
    [LabelText("绘制容差带")] public bool drawTolerance = true;
    [LabelText("容差带半宽")] public float gizmoTolerance = 0.15f;

    /// <summary>路径点（局部坐标）。</summary>
    public List<Vector2> GetLocalPoints()
    {
        if (useEdgeCollider)
        {
            EdgeCollider2D edge = GetComponent<EdgeCollider2D>();
            if (edge != null)
            {
                Vector2[] raw = edge.points;
                List<Vector2> result = new List<Vector2>(raw.Length);
                for (int i = 0; i < raw.Length; i++)
                {
                    result.Add(raw[i] + edge.offset);
                }
                return result;
            }
        }
        return new List<Vector2>(localPoints);
    }

    /// <summary>保留点（世界坐标）。</summary>
    public Vector2 GetKeepPointWorld()
    {
        Vector2 local = keepPointLocal;

        if (autoKeepPoint)
        {
            List<Vector2> points = GetLocalPoints();
            if (points.Count > 0)
            {
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < points.Count; i++)
                {
                    sum += points[i];
                }
                local = sum / points.Count;
            }
        }

        return transform.TransformPoint(local);
    }

    /// <summary>把路径拆成待切的边（世界坐标）。</summary>
    public List<Edge> BuildWorldEdges()
    {
        List<Vector2> local = GetLocalPoints();
        List<Edge> edges = new List<Edge>();

        if (local.Count < 2)
        {
            return edges;
        }

        List<Vector2> world = new List<Vector2>(local.Count);
        for (int i = 0; i < local.Count; i++)
        {
            world.Add(transform.TransformPoint(local[i]));
        }

        int segmentCount = closed ? world.Count : world.Count - 1;
        for (int i = 0; i < segmentCount; i++)
        {
            Vector2 a = world[i];
            Vector2 b = world[(i + 1) % world.Count];

            // 首尾几乎重合的收尾点会产生零长度边，直接跳过
            if (Vector2.Distance(a, b) < 0.001f)
            {
                continue;
            }

            edges.Add(new Edge { index = edges.Count, p = a, q = b });
        }

        return edges;
    }

    /// <summary>把整条路径当成闭合多边形取出来（世界坐标），用于结算时的形状校验。</summary>
    public Polygon2D BuildWorldPolygon()
    {
        List<Vector2> local = GetLocalPoints();
        if (local.Count < 3)
        {
            return null;
        }

        Polygon2D polygon = new Polygon2D();
        for (int i = 0; i < local.Count; i++)
        {
            // 首尾重合的收尾点会让多边形自交，丢掉
            if (i == local.Count - 1 && Vector2.Distance(local[i], local[0]) < 0.2f)
            {
                continue;
            }
            polygon.AddPoint((Vector2)transform.TransformPoint(local[i]));
        }

        return polygon.pointsList.Count >= 3 ? polygon : null;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        List<Edge> edges = BuildWorldEdges();

        for (int i = 0; i < edges.Count; i++)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(edges[i].p, edges[i].q);

            if (drawTolerance && gizmoTolerance > 0f)
            {
                Vector2 dir = (edges[i].q - edges[i].p).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x) * gizmoTolerance;

                Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
                Gizmos.DrawLine(edges[i].p + normal, edges[i].q + normal);
                Gizmos.DrawLine(edges[i].p - normal, edges[i].q - normal);
            }
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(GetKeepPointWorld(), 0.08f);
    }
}
