using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Slicer2D;
using UnityEngine;
using Utilities2D;

// Slicer2D 命名空间里也有 Debug / Input 同名类，不加别名会和 UnityEngine 的撞名
using Debug = UnityEngine.Debug;
using Input = UnityEngine.Input;

/// <summary>切割方式。</summary>
public enum GemCutMode
{
    /// <summary>整刀贯穿。适用于凸轮廓（矩形、四边形、多边形），一刀掉一块。</summary>
    Split,

    /// <summary>沿线挖槽。适用于凹轮廓（星形等），贯穿直线做不出凹角时用这个。</summary>
    Carve
}

/// <summary>一局结算。</summary>
public struct GemCutResult
{
    public int edgeCount;

    /// <summary>没对上任何目标边、但照样切下去的刀数。</summary>
    public int freeCuts;

    public float averageQuality;
    public float minQuality;
    public int stars;

    /// <summary>白色轮廓内被切掉的比例。</summary>
    public float targetDamage;

    /// <summary>是否因为切进目标区域太多而失败。</summary>
    public bool failed;

    /// <summary>是否跑了形状自检。</summary>
    public bool shapeVerified;
    public GemShapeScore shapeScore;
}

/// <summary>
/// 宝石描线切割：判定玩家的每一刀是否沿着目标虚线，通过则吸附到理论线上再真正下刀。
///
/// 为什么判路径而不是判最终形状：每一刀都切在线上，剩下的形状必然是对的。
/// 顺带还能拿到实时反馈（对准了就变绿）和吸附（用理论坐标下刀，几何永远干净）。
/// </summary>
public class GemCutController : MonoBehaviour
{
    [Title("引用")]
    [LabelText("当前宝石")]
    public Sliceable2D gem;

    [LabelText("目标路径")]
    public GemCutTarget target;

    [LabelText("游戏相机")]
    [Tooltip("留空则用 Camera.main。")]
    public Camera gameCamera;

    [Title("工具跟随")]
    [LabelText("工具跟随鼠标")]
    public bool followMouse = true;

    [LabelText("工具根节点")]
    [Tooltip("跟着鼠标跑的节点。留空就用挂着本脚本的物体（GemCutter）。")]
    public Transform toolRoot;

    [LabelText("切割判定点")]
    [Tooltip("真正用来判定和下刀的点，一般是锯齿尖端 CutterPoint。留空则退回直接用鼠标坐标。")]
    public Transform cutPoint;

    [Title("切割方式")]
    [LabelText("模式")]
    [Tooltip("Split：整刀贯穿，凸轮廓用。Carve：沿线挖槽，凹轮廓用。")]
    public GemCutMode cutMode = GemCutMode.Split;

    [LabelText("挖槽宽度")]
    [ShowIf("@this.cutMode == GemCutMode.Carve")]
    [Tooltip("跟虚线的视觉宽度对齐，否则玩家会觉得切多了。")]
    public float carveWidth = 0.06f;

    [Title("判定容差")]
    [LabelText("垂距容差")]
    [Tooltip("玩家切线偏离目标边多远还算对。建议取虚线线宽的一半左右。")]
    public float maxOffset = 0.18f;

    [LabelText("角度容差（度）")]
    public float maxAngleDeg = 14f;

    [LabelText("覆盖缺口容差")]
    [Tooltip("笔画在长度方向可以短这么多。防止玩家只在中间划一小截就算切完整条边。")]
    public float coverSlack = 0.2f;

    [LabelText("最短有效笔画")]
    [Tooltip("比这个还短的拖拽当误触，不判失误。")]
    public float minDragDistance = 0.25f;

    [LabelText("必须按顺序切")]
    public bool requireOrder = false;

    [Title("失败条件")]
    [LabelText("目标区域最大允许损伤")]
    [Tooltip("白色轮廓内的区域被切掉超过这个比例就直接判失败。0.1 = 10%。")]
    public float maxTargetDamage = 0.1f;

    [LabelText("损伤检测分辨率")]
    [Tooltip("每切一刀都会跑一次采样。128 对 10% 这个量级的阈值精度绰绰有余。")]
    public int damageCheckResolution = 128;

    [Title("评星")]
    [LabelText("三星最低质量")] public float threeStarQuality = 0.6f;
    [LabelText("二星最低质量")] public float twoStarQuality = 0.3f;

    [Title("表现")]
    [LabelText("线材质")]
    [Tooltip("留空会自动创建一个。想指定的话拖 Sprite-Unlit-Default 之类的即可。")]
    public Material lineMaterial;

    [LabelText("线宽")] public float lineWidth = 0.05f;
    [LabelText("排序层级")] public int sortingOrder = 100;

    [LabelText("玩家切线颜色")] public Color cutLineColor = new Color(1f, 0.25f, 0.2f, 0.9f);
    [LabelText("对准时切线颜色")] public Color cutLineAlignedColor = new Color(0.3f, 1f, 0.45f, 0.95f);

    [LabelText("待切边颜色")] public Color edgePendingColor = new Color(1f, 1f, 1f, 0.22f);
    [LabelText("对准边颜色")] public Color edgeAlignedColor = new Color(0.3f, 1f, 0.45f, 0.9f);
    [LabelText("已切边颜色")] public Color edgeDoneColor = new Color(0.25f, 0.9f, 1f, 0.45f);

    [Title("碎块")]
    [LabelText("弹开力度")] public float debrisForce = 3f;
    [LabelText("旋转力度")] public float debrisTorque = 2f;
    [LabelText("重力倍数")] public float debrisGravity = 2f;
    [LabelText("存活时间")] public float debrisLifetime = 1.5f;

    [Title("调试")]
    [LabelText("显示调试信息")] public bool showDebugHud = true;

    [LabelText("测试笔画偏移")]
    [Tooltip("下面的测试按钮会让模拟笔画垂直偏离目标边这么多，用来试容差边界。")]
    public float debugStrokeOffset = 0f;

    [LabelText("结算时跑形状自检")]
    [Tooltip("用栅格采样算一遍 IoU，验证路径判定确实产出了正确形状。只在结算时跑一次。")]
    public bool verifyShapeOnComplete = true;

    /// <summary>当前判定点的世界坐标（锯齿尖端）。做特效、拖尾、UI 提示时用。</summary>
    public Vector2 CurrentCutPoint
    {
        get { return GetCutSamplePoint(); }
    }

    /// <summary>切掉一条边：边索引、质量 0~1。</summary>
    public event Action<int, float> EdgeCut;

    /// <summary>这一刀没对上任何目标边，但照样切了。</summary>
    public event Action FreeCut;

    /// <summary>切进白色轮廓内太多，判失败。参数是损伤比例。</summary>
    public event Action<float> Failed;

    /// <summary>全部切完。</summary>
    public event Action<GemCutResult> Completed;

    private readonly List<GemCutTarget.Edge> edges = new List<GemCutTarget.Edge>();
    private bool[] edgeDone;
    private float[] edgeQuality;
    private int freeCuts;
    private bool finished;
    private bool failed;
    private float targetDamage;

    private bool dragging;
    private Vector2 dragStart;
    private Vector2 dragEnd;
    private int alignedEdge = -1;
    private GemCutJudgement alignedJudgement;

    // 调参用：当前笔画离哪条边最近、差多少
    private int closestEdge = -1;
    private GemCutJudgement closestJudgement;

    private Transform visualRoot;
    private LineRenderer cutLine;
    private readonly List<LineRenderer> edgeLines = new List<LineRenderer>();
    private Material runtimeLineMaterial;

    private GameObject gemBackup;

    // 屏幕转世界需要一个平面深度。取宝石所在的 z 平面 —— 不能再用本物体的 z，
    // 因为工具根节点现在会跟着鼠标跑。
    private float cutPlaneZ;

    private void Start()
    {
        if (gameCamera == null)
        {
            gameCamera = Camera.main;
        }

        if (toolRoot == null)
        {
            toolRoot = transform;
        }

        cutPlaneZ = gem != null ? gem.transform.position.z : 0f;

        CreateBackup();
        Begin();
    }

    private void Update()
    {
        // 必须排在取判定点之前，否则这一帧读到的还是上一帧的判定点位置
        UpdateToolFollow();

        if (finished)
        {
            UpdateVisuals();
            return;
        }

        HandleInput();
        UpdateVisuals();
    }

    /// <summary>让整套工具（锯子 + 手）跟着鼠标走，判定点作为子节点自然跟着偏移。</summary>
    private void UpdateToolFollow()
    {
        if (!followMouse || toolRoot == null || gameCamera == null)
        {
            return;
        }

        Vector2 mouse = GetMouseWorld();
        toolRoot.position = new Vector3(mouse.x, mouse.y, toolRoot.position.z);
    }

    /// <summary>重新构建这一局的判定状态。</summary>
    [Button("重新开始（会还原宝石）")]
    public void Restart()
    {
        if (gem != null)
        {
            Destroy(gem.gameObject);
            gem = null;
        }

        if (gemBackup != null)
        {
            GameObject fresh = Instantiate(gemBackup, gemBackup.transform.parent);
            fresh.name = gemBackup.name.Replace(" (Backup)", "");
            fresh.SetActive(true);
            gem = fresh.GetComponent<Sliceable2D>();

            // Sliceable2D 的 spriteRenderer 是在 Start -> Initialize 里才填的。
            // 不在这里补一次的话，同一帧就下刀会在 SpriteToMesh 里炸空引用。
            if (gem != null)
            {
                gem.Initialize();
            }
        }

        Begin();
    }

    private void Begin()
    {
        edges.Clear();
        if (target != null)
        {
            edges.AddRange(target.BuildWorldEdges());
        }

        edgeDone = new bool[edges.Count];
        edgeQuality = new float[edges.Count];

        freeCuts = 0;
        failed = false;
        targetDamage = 0f;
        finished = edges.Count == 0;
        dragging = false;
        alignedEdge = -1;
        closestEdge = -1;

        FreezeGem(gem);
        BuildVisuals();

        if (edges.Count == 0)
        {
            Debug.LogWarning("[GemCut] 目标路径为空，检查 GemCutTarget 是否挂在带 EdgeCollider2D 的物体上。", this);
        }
    }

    private void CreateBackup()
    {
        if (gem == null || gemBackup != null)
        {
            return;
        }

        gemBackup = Instantiate(gem.gameObject, gem.transform.parent);
        gemBackup.SetActive(false);
        gemBackup.name = gem.gameObject.name + " (Backup)";
    }

    // ---------------- 输入 ----------------

    private void HandleInput()
    {
        if (gameCamera == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            dragging = true;
            dragStart = GetCutSamplePoint();
            dragEnd = dragStart;
        }

        if (dragging)
        {
            dragEnd = GetCutSamplePoint();
            alignedEdge = FindBestEdge(dragStart, dragEnd, out alignedJudgement);
        }

        if (dragging && Input.GetMouseButtonUp(0))
        {
            dragging = false;
            dragEnd = GetCutSamplePoint();
            Release();
        }
    }

    /// <summary>
    /// 判定和下刀用的点：锯齿尖端 CutterPoint，不是鼠标本身。
    /// 没配 cutPoint 就退回鼠标坐标（老行为）。
    /// </summary>
    private Vector2 GetCutSamplePoint()
    {
        return cutPoint != null ? (Vector2)cutPoint.position : GetMouseWorld();
    }

    private Vector2 GetMouseWorld()
    {
        Vector3 screen = Input.mousePosition;
        screen.z = Mathf.Abs(gameCamera.transform.position.z - cutPlaneZ);
        return gameCamera.ScreenToWorldPoint(screen);
    }

    private void Release()
    {
        if (finished)
        {
            return;
        }

        if (Vector2.Distance(dragStart, dragEnd) < minDragDistance)
        {
            // 点一下当误触，不惩罚
            alignedEdge = -1;
            return;
        }

        alignedEdge = FindBestEdge(dragStart, dragEnd, out alignedJudgement);

        if (alignedEdge >= 0)
        {
            // 对上了某条目标边：吸附到理论坐标下刀，记进度和质量
            PerformSnappedCut(alignedEdge, alignedJudgement.quality);
        }
        else
        {
            // 没对上也照切。路径判定现在只决定这一刀算不算进度，不再决定能不能切
            freeCuts++;
            FreeCut?.Invoke();

            CutAlong(dragStart, dragEnd);
            CheckTargetDamage();
        }

        alignedEdge = -1;
    }

    /// <summary>
    /// 用一段世界坐标的笔画走一遍完整的判定 + 下刀流程，不经过鼠标输入。
    /// 调试、自动化测试、或者做“提示/自动完成”功能时用。
    /// </summary>
    public void SimulateStroke(Vector2 worldFrom, Vector2 worldTo)
    {
        dragging = false;
        dragStart = worldFrom;
        dragEnd = worldTo;
        Release();
    }

    [Button("测试：照着下一条待切边切一刀")]
    public void DebugCutNextEdge()
    {
        int index = NextPendingIndex();
        if (index < 0)
        {
            Debug.Log("[GemCut] 没有待切的边了。", this);
            return;
        }

        GemCutTarget.Edge edge = edges[index];
        Vector2 dir = (edge.q - edge.p).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * debugStrokeOffset;

        // 模拟真人划动：两端各超出一点
        SimulateStroke(edge.p - dir * 0.3f + normal, edge.q + dir * 0.3f + normal);
    }

    [Button("测试：横穿目标区域切一刀（应判失败）")]
    public void DebugCutThroughTarget()
    {
        if (target == null)
        {
            return;
        }

        // 从轮廓中心横着划过去，必然切掉一大块轮廓内的料
        Vector2 center = target.GetKeepPointWorld();
        SimulateStroke(center + Vector2.left * 3f, center + Vector2.right * 3f);
    }

    [Button("测试：一键切完")]
    public void DebugCutAll()
    {
        for (int guard = 0; guard < 64 && NextPendingIndex() >= 0; guard++)
        {
            DebugCutNextEdge();
        }
    }

    /// <summary>在所有未切的边里找匹配得最好的那条。顺带记录最接近的一条用于调参。</summary>
    private int FindBestEdge(Vector2 a, Vector2 b, out GemCutJudgement best)
    {
        best = default(GemCutJudgement);

        int bestIndex = -1;
        float bestQuality = -1f;

        closestEdge = -1;
        float closestOffset = float.MaxValue;

        int orderedNext = requireOrder ? NextPendingIndex() : -1;

        for (int i = 0; i < edges.Count; i++)
        {
            if (edgeDone[i])
            {
                continue;
            }

            if (requireOrder && i != orderedNext)
            {
                continue;
            }

            GemCutJudgement judgement = GemCutPathMatcher.Judge(
                edges[i].p, edges[i].q, a, b, maxOffset, maxAngleDeg, coverSlack);

            if (judgement.offsetError < closestOffset)
            {
                closestOffset = judgement.offsetError;
                closestEdge = i;
                closestJudgement = judgement;
            }

            if (judgement.pass && judgement.quality > bestQuality)
            {
                bestQuality = judgement.quality;
                bestIndex = i;
                best = judgement;
            }
        }

        return bestIndex;
    }

    private int NextPendingIndex()
    {
        for (int i = 0; i < edges.Count; i++)
        {
            if (!edgeDone[i])
            {
                return i;
            }
        }
        return -1;
    }

    // ---------------- 切割 ----------------

    /// <summary>
    /// 判定通过后不用玩家的原始坐标下刀，改用目标边的精确坐标。
    /// 这样几何永远干净（不会切出 0.01 宽的碎片），结果和虚线像素级吻合，容差还能放宽。
    /// </summary>
    private void PerformSnappedCut(int index, float quality)
    {
        GemCutTarget.Edge edge = edges[index];

        bool changed = CutAlong(edge.p, edge.q);
        if (!changed && showDebugHud)
        {
            Debug.LogWarning(string.Format("[GemCut] 第 {0} 条边判定通过但没切出新几何，可能这块料已经被切掉了。", index), this);
        }

        edgeDone[index] = true;
        edgeQuality[index] = quality;

        EdgeCut?.Invoke(index, quality);

        // 吸附的刀理论上不会伤到目标区域，但保留点设错、或者之前已经被自由刀削过，
        // 都可能让这一刀真的切进去，所以统一检一遍
        if (CheckTargetDamage())
        {
            return;
        }

        if (NextPendingIndex() < 0)
        {
            Complete();
        }
    }

    /// <summary>
    /// 算一遍白色轮廓内的区域被切掉了多少。超过上限就直接判失败。
    /// 返回是否已经判失败。
    /// </summary>
    private bool CheckTargetDamage()
    {
        if (target == null)
        {
            return false;
        }

        Polygon2D targetPolygon = target.BuildWorldPolygon();
        if (targetPolygon == null)
        {
            return false;
        }

        List<Polygon2D> remaining = new List<Polygon2D>();
        Polygon2D gemPolygon = gem != null ? GetWorldPolygon(gem.gameObject) : null;
        if (gemPolygon != null)
        {
            remaining.Add(gemPolygon);
        }

        float coverage = GemShapeMatcher.EvaluateCoverage(targetPolygon, remaining, damageCheckResolution);
        targetDamage = Mathf.Clamp01(1f - coverage);

        if (targetDamage <= maxTargetDamage)
        {
            return false;
        }

        Fail();
        return true;
    }

    private void Fail()
    {
        failed = true;
        finished = true;

        Debug.LogWarning(string.Format(
            "[GemCut] 失败：切进白色轮廓内的区域已达 {0:P1}，超过上限 {1:P0}。已切对 {2}/{3} 条边，自由刀 {4} 刀。",
            targetDamage, maxTargetDamage, DoneCount(), edges.Count, freeCuts), this);

        Failed?.Invoke(targetDamage);
    }

    private int DoneCount()
    {
        int count = 0;
        for (int i = 0; i < edgeDone.Length; i++)
        {
            if (edgeDone[i])
            {
                count++;
            }
        }
        return count;
    }

    private bool CutAlong(Vector2 p, Vector2 q)
    {
        if (gem == null)
        {
            return false;
        }

        Slice2D result;

        if (cutMode == GemCutMode.Split)
        {
            Polygon2D world = GetWorldPolygon(gem.gameObject);
            if (world == null)
            {
                return false;
            }

            // 目标边只是宝石内部的一条弦，要延长到完全贯穿，
            // 否则端点落在多边形内部，插件会报 Incorrect Split
            Rect bounds = world.GetBounds();
            float extend = new Vector2(bounds.width, bounds.height).magnitude + 1f;

            Vector2 dir = (q - p).normalized;
            Vector2 mid = (p + q) * 0.5f;

            result = gem.LinearSlice(new Pair2D(mid - dir * extend, mid + dir * extend));
        }
        else
        {
            result = gem.LinearCutSlice(LinearCut.Create(new Pair2(p, q), carveWidth));
        }

        return AdoptResult(result, p, q);
    }

    /// <summary>切完之后从碎块里挑出宝石本体，其余当废料弹走。</summary>
    private bool AdoptResult(Slice2D result, Vector2 cutP, Vector2 cutQ)
    {
        if (result == null)
        {
            return false;
        }

        List<GameObject> pieces = result.GetGameObjects();
        if (pieces == null || pieces.Count == 0)
        {
            return false;
        }

        Vector2 keepWorld = target != null ? target.GetKeepPointWorld() : (cutP + cutQ) * 0.5f;
        Vector2D keepPoint = new Vector2D(keepWorld);

        GameObject keeper = null;
        float keeperArea = -1f;

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] == null)
            {
                continue;
            }

            Polygon2D poly = GetWorldPolygon(pieces[i]);
            if (poly == null)
            {
                continue;
            }

            if (poly.PointInPoly(keepPoint))
            {
                float area = (float)poly.GetArea();
                if (area > keeperArea)
                {
                    keeper = pieces[i];
                    keeperArea = area;
                }
            }
        }

        // 兜底：没有任何碎块包含保留点（保留点没设对，或者被切掉了），留最大的那块
        if (keeper == null)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null)
                {
                    continue;
                }

                Polygon2D poly = GetWorldPolygon(pieces[i]);
                float area = poly != null ? (float)poly.GetArea() : 0f;
                if (area > keeperArea)
                {
                    keeper = pieces[i];
                    keeperArea = area;
                }
            }
        }

        if (keeper == null)
        {
            return false;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null && pieces[i] != keeper)
            {
                MakeDebris(pieces[i], cutP, cutQ);
            }
        }

        gem = keeper.GetComponent<Sliceable2D>();
        FreezeGem(gem);

        return true;
    }

    private void MakeDebris(GameObject piece, Vector2 cutP, Vector2 cutQ)
    {
        Polygon2D poly = GetWorldPolygon(piece);

        Rigidbody2D body = piece.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = debrisGravity;

            Vector2 dir = (cutQ - cutP).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);

            Vector2 center = poly != null ? poly.GetBounds().center : (Vector2)piece.transform.position;
            float side = Mathf.Sign(Vector2.Dot(center - cutP, normal));
            if (Mathf.Approximately(side, 0f))
            {
                side = 1f;
            }

            body.AddForce(normal * side * debrisForce, ForceMode2D.Impulse);
            body.AddTorque(UnityEngine.Random.Range(-debrisTorque, debrisTorque), ForceMode2D.Impulse);
        }

        // 别让废料继续参与判定和切割
        Sliceable2D sliceable = piece.GetComponent<Sliceable2D>();
        if (sliceable != null)
        {
            sliceable.enabled = false;
        }

        Collider2D collider = piece.GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Destroy(piece, debrisLifetime);
    }

    private void FreezeGem(Sliceable2D sliceable)
    {
        if (sliceable == null)
        {
            return;
        }

        Rigidbody2D body = sliceable.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private static Polygon2D GetWorldPolygon(GameObject piece)
    {
        if (piece == null)
        {
            return null;
        }

        Sliceable2D sliceable = piece.GetComponent<Sliceable2D>();
        if (sliceable != null && sliceable.shape != null)
        {
            Polygon2D world = sliceable.shape.GetWorld();
            if (world != null && world.pointsList.Count >= 3)
            {
                return world;
            }
        }

        List<Polygon2D> local = Polygon2DList.CreateFromGameObject(piece);
        return local.Count > 0 ? local[0].ToWorldSpace(piece.transform) : null;
    }

    // ---------------- 结算 ----------------

    private void Complete()
    {
        finished = true;

        GemCutResult result = new GemCutResult
        {
            edgeCount = edges.Count,
            freeCuts = freeCuts,
            targetDamage = targetDamage,
            failed = failed,
            minQuality = 1f
        };

        float sum = 0f;
        for (int i = 0; i < edgeQuality.Length; i++)
        {
            sum += edgeQuality[i];
            result.minQuality = Mathf.Min(result.minQuality, edgeQuality[i]);
        }
        result.averageQuality = edgeQuality.Length > 0 ? sum / edgeQuality.Length : 0f;

        int stars = 3;
        if (freeCuts > 0)
        {
            stars--;
        }
        if (result.minQuality < threeStarQuality)
        {
            stars--;
        }
        if (result.minQuality < twoStarQuality)
        {
            stars--;
        }
        result.stars = Mathf.Clamp(stars, 0, 3);

        if (verifyShapeOnComplete && target != null)
        {
            Polygon2D targetPolygon = target.BuildWorldPolygon();
            Polygon2D gemPolygon = gem != null ? GetWorldPolygon(gem.gameObject) : null;

            if (targetPolygon != null && gemPolygon != null)
            {
                List<Polygon2D> remaining = new List<Polygon2D> { gemPolygon };
                result.shapeScore = GemShapeMatcher.Evaluate(targetPolygon, remaining);
                result.shapeVerified = true;
            }
        }

        if (showDebugHud)
        {
            Debug.Log(string.Format("[GemCut] 完成：{0} 星，平均质量 {1:P0}，最差 {2:P0}，自由刀 {3} 刀，目标区域损伤 {4:P1}。{5}",
                result.stars, result.averageQuality, result.minQuality, result.freeCuts, result.targetDamage,
                result.shapeVerified ? "形状自检 " + result.shapeScore : ""), this);
        }

        Completed?.Invoke(result);
    }

    // ---------------- 表现 ----------------

    private void BuildVisuals()
    {
        if (visualRoot == null)
        {
            // 放在场景根节点而不是本物体下面：本物体现在跟着鼠标跑，
            // 线用的是世界坐标，挂在会动的父节点下面只会让人看着困惑。
            GameObject root = new GameObject("GemCutVisuals");
            visualRoot = root.transform;
        }

        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(visualRoot.GetChild(i).gameObject);
        }

        edgeLines.Clear();
        for (int i = 0; i < edges.Count; i++)
        {
            LineRenderer line = CreateLine("EdgeLine_" + i, sortingOrder);
            line.SetPosition(0, edges[i].p);
            line.SetPosition(1, edges[i].q);
            edgeLines.Add(line);
        }

        cutLine = CreateLine("CutLine", sortingOrder + 1);
        cutLine.enabled = false;
    }

    private LineRenderer CreateLine(string name, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(visualRoot, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        line.sortingOrder = order;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        return line;
    }

    private Material GetLineMaterial()
    {
        if (lineMaterial != null)
        {
            return lineMaterial;
        }

        if (runtimeLineMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            runtimeLineMaterial = new Material(shader);
        }

        return runtimeLineMaterial;
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < edgeLines.Count && i < edges.Count; i++)
        {
            LineRenderer line = edgeLines[i];
            if (line == null)
            {
                continue;
            }

            Color color = edgeDone[i]
                ? edgeDoneColor
                : (i == alignedEdge ? edgeAlignedColor : edgePendingColor);

            line.startColor = color;
            line.endColor = color;
        }

        if (cutLine == null)
        {
            return;
        }

        cutLine.enabled = dragging;
        if (dragging)
        {
            Color color = alignedEdge >= 0 ? cutLineAlignedColor : cutLineColor;
            cutLine.startColor = color;
            cutLine.endColor = color;
            cutLine.SetPosition(0, dragStart);
            cutLine.SetPosition(1, dragEnd);
        }
    }

    private void OnGUI()
    {
        if (!showDebugHud)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(10f, 10f, 380f, 400f));

        string state0 = failed ? "已失败" : (finished ? "已完成" : "进行中");
        GUILayout.Label(string.Format("模式 {0}    自由刀 {1}    {2}", cutMode, freeCuts, state0));

        GUILayout.Label(string.Format("目标区域损伤 {0:P1}  /  上限 {1:P0}", targetDamage, maxTargetDamage));

        for (int i = 0; i < edges.Count; i++)
        {
            string state = edgeDone[i]
                ? string.Format("已切  质量 {0:P0}", edgeQuality[i])
                : (i == alignedEdge ? "→ 对准了，抬手会吸附到这条边" : "待切");

            GUILayout.Label(string.Format("边 {0}：{1}", i, state));
        }

        if (dragging && closestEdge >= 0)
        {
            GUILayout.Space(6f);
            GUILayout.Label(string.Format("最近的边 {0}：垂距 {1:F3} / 容差 {2:F3}",
                closestEdge, closestJudgement.offsetError, maxOffset));
            GUILayout.Label(string.Format("夹角 {0:F1}° / 容差 {1:F1}°    长度覆盖 {2}",
                closestJudgement.angleError, maxAngleDeg, closestJudgement.covered ? "够" : "不够"));
        }

        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (runtimeLineMaterial != null)
        {
            Destroy(runtimeLineMaterial);
        }

        // 视觉节点现在挂在场景根上，得自己收掉
        if (visualRoot != null)
        {
            Destroy(visualRoot.gameObject);
        }
    }
}
