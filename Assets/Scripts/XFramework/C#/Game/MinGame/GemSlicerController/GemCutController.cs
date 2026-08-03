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

    /// <summary>
    /// 自由轨迹。按玩家实际拖出来的折线切，可以拐弯、画 V 形、画弧线。
    /// Split 只认「起点到终点」的那条直线，这个模式认整条轨迹。
    /// </summary>
    Complex
}

/// <summary>一局结算。</summary>
public struct GemCutResult
{
    public int edgeCount;

    /// <summary>没对上任何目标边、但照样切下去的刀数。</summary>
    public int freeCuts;

    /// <summary>已经切对的目标边数量。</summary>
    public int edgesCut;

    /// <summary>最终得分，0~100。越贴近白色轮廓越高；切进轮廓超过阈值直接 0。</summary>
    public int score;

    /// <summary>白色轮廓内被切掉的比例。</summary>
    public float targetDamage;

    /// <summary>是否因为切进目标区域太多而判 0 分。</summary>
    public bool failed;

    /// <summary>是否通关：没切坏，且分数达到胜利分数线。</summary>
    public bool win;

    /// <summary>剩余形状与目标轮廓的重合度明细，用于调试和展示。</summary>
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
    [Title("运行时引用（由 SetData 填，不用手拖）")]
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

    [LabelText("结算后隐藏工具")]
    [Tooltip("一局结束（通关或切坏）后把刀具藏起来。关掉则只是停止跟随、退回初始位置，刀子仍然可见。")]
    public bool hideToolOnFinish = true;

    [LabelText("工具根节点")]
    [Tooltip("跟着鼠标跑的节点。留空就用挂着本脚本的物体（GemCutter）。")]
    public Transform toolRoot;

    [LabelText("切割判定点")]
    [Tooltip("真正用来判定和下刀的点，一般是锯齿尖端 CutterPoint。留空则退回直接用鼠标坐标。")]
    public Transform cutPoint;

    [Title("切割方式")]
    [LabelText("模式")]
    [Tooltip("Split：只认起点到终点的直线，整刀贯穿。Complex：按玩家拖出来的整条折线切。")]
    public GemCutMode cutMode = GemCutMode.Complex;

    [LabelText("轨迹采样间距")]
    [ShowIf("@this.cutMode == GemCutMode.Complex")]
    [Tooltip("拖拽轨迹按这个间距重采样。太小会让求交算法出问题（插件内部精度 0.1），太大则拐弯会被抹平。")]
    public float trailPointSpacing = 0.3f;

    [LabelText("轨迹最大点数")]
    [ShowIf("@this.cutMode == GemCutMode.Complex")]
    [Tooltip("兜底上限，防止玩家一直画导致轨迹无限增长。")]
    public int trailMaxPoints = 256;

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
    [Tooltip("白色轮廓内的区域被切掉超过这个比例就直接判 0 分。0.05 = 5%。")]
    public float maxTargetDamage = 0.05f;

    [LabelText("重合度采样分辨率")]
    [Tooltip("每切一刀跑一次采样，损伤和评分都用它。128 对 10% 这个量级的阈值精度绰绰有余。")]
    public int damageCheckResolution = 128;

    [Title("评分")]
    [LabelText("胜利分数线")]
    [Tooltip("每切一刀算一次分，一旦达到这个分数就当场判胜利。低于它则要玩家点「切割石头」交卷。")]
    public int winScore = 80;

    [LabelText("每刀打印当前评分")]
    [Tooltip("每切一刀就在 Console 里输出一次当前分数和损伤，方便调参和观察。")]
    public bool logScoreEachCut = true;

    [Title("表现")]
    [LabelText("切割轨迹线")]
    [Tooltip("挂在刀子下面的 LineRenderer，用来画玩家正在切的那条线。" +
             "材质、线宽、颜色、排序全部在这个组件上自己调，脚本只负责喂坐标和开关显隐。")]
    public LineRenderer cutTrailLine;

    [Title("碎块")]
    [LabelText("弹开力度")] public float debrisForce = 3f;
    [LabelText("旋转力度")] public float debrisTorque = 2f;
    [LabelText("重力倍数")] public float debrisGravity = 2f;
    [LabelText("存活时间")] public float debrisLifetime = 1.5f;

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

    // 没调 SetData 之前不接受任何输入
    private bool finished = true;
    private bool failed;
    private float targetDamage;

    // 每切一刀刷新一次，结算直接复用，保证「当前评分」和最终得分完全一致
    private GemShapeScore currentShape;
    private int currentScore;

    private bool dragging;
    private Vector2 dragStart;
    private Vector2 dragEnd;

    // 玩家拖出来的完整轨迹（已按 trailPointSpacing 重采样）。Complex 模式下真正用来下刀的就是它
    private readonly List<Vector2> trail = new List<Vector2>();
    private int alignedEdge = -1;
    private GemCutJudgement alignedJudgement;


    private GameObject gemBackup;

    // 屏幕转世界需要一个平面深度。取宝石所在的 z 平面 —— 不能再用本物体的 z，
    // 因为工具根节点现在会跟着鼠标跑。
    private float cutPlaneZ;

    // 刀具的初始摆放位置和表现节点，结算收刀时用。只在第一次 CacheRefs 时记，
    // 之后 toolRoot 已经被鼠标带跑了，再记就成了光标位置。
    private Renderer[] toolRenderers;
    private Vector3 toolHomePosition;
    private bool toolHomeCached;

    private void Start()
    {
        CacheRefs();
    }

    /// <summary>
    /// 接入正式流程的入口：宝石是按衣服动态生成的，生成完调这个开一局。
    /// 反复调用即可换宝石重开，旧宝石的备份会一并清掉。
    /// </summary>
    public void SetData(GameSmartData smartData)
    {
        if (smartData == null)
        {
            Debug.LogError("[GemCut] SetData 传入的 GameSmartData 为空。", this);
            return;
        }

        if (smartData.Gem == null || smartData.GemCutTarget == null)
        {
            Debug.LogError(string.Format(
                "[GemCut] 宝石预制体 {0} 上没找齐组件：Sliceable2D={1}，GemCutTarget={2}。检查预制体，并确认 GameSmartData.Init() 已调用。",
                smartData.name, smartData.Gem != null, smartData.GemCutTarget != null), this);
            return;
        }

        CacheRefs();

        gem = smartData.Gem;
        target = smartData.GemCutTarget;
        cutPlaneZ = gem.transform.position.z;

        // 宝石是这一帧刚生成的，Sliceable2D.Start() 还没跑，spriteRenderer 还是空的。
        // 不在这里补一次，同一帧就下刀会在 SpriteToMesh 里炸空引用。
        gem.Initialize();

        // 换宝石了，上一局的备份作废
        if (gemBackup != null)
        {
            Destroy(gemBackup);
            gemBackup = null;
        }

        CreateBackup();
        Begin();
    }

    private void CacheRefs()
    {
        if (gameCamera == null)
        {
            gameCamera = Camera.main;
        }

        if (toolRoot == null)
        {
            toolRoot = transform;
        }

        if (!toolHomeCached)
        {
            toolHomePosition = toolRoot.position;
            // 宝石挂在 SmartRootTran 下面，不在 toolRoot 里，不会被一起藏掉
            toolRenderers = toolRoot.GetComponentsInChildren<Renderer>(true);
            toolHomeCached = true;
        }
    }

    private void Update()
    {
        // 必须排在取判定点之前，否则这一帧读到的还是上一帧的判定点位置
        UpdateToolFollow();

        if (finished)
        {
            UpdateCutTrailLine();
            return;
        }

        HandleInput();
        UpdateCutTrailLine();
    }

    /// <summary>让整套工具（锯子 + 手）跟着鼠标走，判定点作为子节点自然跟着偏移。</summary>
    private void UpdateToolFollow()
    {
        // 结算之后（以及还没开局时）不再跟随，否则完成弹窗上还有一把刀黏着光标跑
        if (finished || !followMouse || toolRoot == null || gameCamera == null)
        {
            return;
        }

        Vector2 mouse = GetMouseWorld();
        toolRoot.position = new Vector3(mouse.x, mouse.y, toolRoot.position.z);
    }

    /// <summary>开局：把刀具放出来，重新跟随鼠标。</summary>
    private void ShowTool()
    {
        SetToolRenderersEnabled(true);
    }

    /// <summary>
    /// 结算：刀具退回初始摆放位置并（可选）隐藏。
    /// 不跟随这件事由 UpdateToolFollow 的 finished 判断保证，这里只管表现。
    /// </summary>
    private void ParkTool()
    {
        if (toolRoot != null && toolHomeCached)
        {
            toolRoot.position = toolHomePosition;
        }

        if (hideToolOnFinish)
        {
            SetToolRenderersEnabled(false);
        }
    }

    private void SetToolRenderersEnabled(bool isEnabled)
    {
        if (toolRenderers == null)
        {
            return;
        }

        for (int i = 0; i < toolRenderers.Length; i++)
        {
            Renderer renderer = toolRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            // 切割轨迹线的显隐由 UpdateCutTrailLine 每帧自己管，别在这里抢
            if (cutTrailLine != null && renderer == cutTrailLine)
            {
                continue;
            }

            renderer.enabled = isEnabled;
        }
    }

    /// <summary>
    /// 玩家主动交卷：不管切成什么样，立刻按当前形状结算。
    /// 给 UI 上的「完成」按钮用 —— 削不到自动完成的阈值时，玩家总得有个收场的办法。
    /// </summary>
    public void Finish()
    {
        if (finished)
        {
            return;
        }

        // 还没切过任何一刀的话 currentShape 是空的，先算一次再结算
        if (currentScore == 0 && DoneCount() == 0 && freeCuts == 0)
        {
            EvaluateAfterCut();
        }

        Complete();
    }

    /// <summary>用同一块宝石重开一局（还原到 SetData 时的状态）。</summary>
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
        currentShape = default(GemShapeScore);
        currentScore = 0;
        trail.Clear();
        finished = edges.Count == 0;
        dragging = false;
        alignedEdge = -1;

        FreezeGem(gem);
        ShowTool();

        if (cutTrailLine != null)
        {
            cutTrailLine.enabled = false;
        }

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

            trail.Clear();
            trail.Add(dragStart);
        }

        if (dragging)
        {
            dragEnd = GetCutSamplePoint();
            AppendTrail(dragEnd);

            // 匹配目标边永远只看「起点到终点」的直线：目标边本来就是直的，
            // 玩家沿着它划出来的轨迹也必然接近直线
            alignedEdge = FindBestEdge(dragStart, dragEnd, out alignedJudgement);
        }

        if (dragging && Input.GetMouseButtonUp(0))
        {
            dragging = false;
            dragEnd = GetCutSamplePoint();
            AppendTrail(dragEnd);
            Release();
        }
    }

    /// <summary>
    /// 按固定间距往轨迹里补点。直接把每帧的鼠标位置塞进去不行：
    /// 帧率高时点会挤在一起（插件求交精度只有 0.1，点太近会算错），
    /// 帧率低或者划得快时又会漏掉中间一大段。所以沿着方向按固定步长补。
    /// </summary>
    private void AppendTrail(Vector2 position)
    {
        if (trail.Count == 0)
        {
            trail.Add(position);
            return;
        }

        float spacing = Mathf.Max(trailPointSpacing, 0.05f);
        Vector2 last = trail[trail.Count - 1];

        while (Vector2.Distance(last, position) > spacing && trail.Count < trailMaxPoints)
        {
            last += (position - last).normalized * spacing;
            trail.Add(last);
        }
    }

    /// <summary>轨迹的实际长度。V 形回头的笔画不能只看首尾距离。</summary>
    private float GetTrailLength()
    {
        float length = 0f;
        for (int i = 1; i < trail.Count; i++)
        {
            length += Vector2.Distance(trail[i - 1], trail[i]);
        }
        return length;
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

        // 用轨迹长度而不是首尾距离：V 形折回的笔画首尾可能挨得很近，但它是一刀有效的切割
        if (GetTrailLength() < minDragDistance)
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

            if (cutMode == GemCutMode.Complex)
            {
                // 自由轨迹：按玩家实际拖出来的折线切
                CutAlongTrail();
            }
            else
            {
                CutAlong(dragStart, dragEnd);
            }

            EvaluateAfterCut();
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

    /// <summary>在所有未切的边里找匹配得最好的那条。</summary>
    private int FindBestEdge(Vector2 a, Vector2 b, out GemCutJudgement best)
    {
        best = default(GemCutJudgement);

        int bestIndex = -1;
        float bestQuality = -1f;

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
        if (!changed)
        {
            Debug.LogWarning(string.Format("[GemCut] 第 {0} 条边判定通过但没切出新几何，可能这块料已经被切掉了。", index), this);
        }

        edgeDone[index] = true;
        edgeQuality[index] = quality;

        EdgeCut?.Invoke(index, quality);

        // 吸附的刀理论上不会伤到目标区域，但保留点设错、或者之前已经被自由刀削过，
        // 都可能让这一刀真的切进去，所以统一检一遍
        if (EvaluateAfterCut())
        {
            return;
        }

        if (NextPendingIndex() < 0)
        {
            Complete();
        }
    }

    /// <summary>
    /// 每切完一刀跑一次：重算剩余形状与目标轮廓的重合度，得出当前评分和轮廓内损伤。
    /// 损伤超过上限就直接判 0 分结束。返回是否已经判失败。
    /// </summary>
    private bool EvaluateAfterCut()
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

        currentShape = GemShapeMatcher.Evaluate(targetPolygon, remaining, damageCheckResolution);
        targetDamage = Mathf.Clamp01(1f - currentShape.coverage);

        bool overDamage = targetDamage > maxTargetDamage;
        currentScore = overDamage ? 0 : Mathf.Clamp(Mathf.RoundToInt(currentShape.iou * 100f), 0, 100);

        if (logScoreEachCut)
        {
            Debug.Log(string.Format(
                "[GemCut] 当前评分 {0} 分{1}    轮廓内损伤 {2:P1}（上限 {3:P0}）    {4}    已切对 {5}/{6} 条边，自由刀 {7} 刀",
                currentScore,
                overDamage ? "  ←已超损伤上限，判 0" : "",
                targetDamage, maxTargetDamage, currentShape,
                DoneCount(), edges.Count, freeCuts), this);
        }

        if (overDamage)
        {
            Fail();
            return true;
        }

        // 切到及格分就当场判胜利，不用玩家再去点「切割石头」交卷
        if (currentScore >= winScore)
        {
            Debug.Log(string.Format("[GemCut] 达到及格分 {0}，直接判胜利。", winScore), this);
            Complete();
            return true;
        }

        return false;
    }

    private void Fail()
    {
        failed = true;

        Debug.LogWarning(string.Format(
            "[GemCut] 切坏了：切进白色轮廓内的区域已达 {0:P1}，超过上限 {1:P0}，本局判 0 分。已切对 {2}/{3} 条边，自由刀 {4} 刀。",
            targetDamage, maxTargetDamage, DoneCount(), edges.Count, freeCuts), this);

        Failed?.Invoke(targetDamage);

        // 失败也走同一个结算出口，保证 Completed 每局必定只触发一次
        Complete();
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

    /// <summary>
    /// 沿一条直线整刀贯穿。
    /// Complex 模式下只有「吸附到目标边」的刀走这里，自由刀走 CutAlongTrail。
    /// </summary>
    private bool CutAlong(Vector2 p, Vector2 q)
    {
        if (gem == null)
        {
            return false;
        }

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

        Slice2D result = gem.LinearSlice(new Pair2D(mid - dir * extend, mid + dir * extend));

        return AdoptResult(result, p, q);
    }

    /// <summary>
    /// 沿玩家拖出来的整条折线下刀。折线拐弯、画 V、画弧都能切。
    /// 前提是轨迹要真的横穿宝石：起点和终点都落在宝石内部的话，插件切不出结果。
    /// </summary>
    private bool CutAlongTrail()
    {
        if (gem == null || trail.Count < 2)
        {
            return false;
        }

        List<Vector2D> slice = new List<Vector2D>(trail.Count);
        for (int i = 0; i < trail.Count; i++)
        {
            slice.Add(new Vector2D(trail[i]));
        }

        // 这是个静态字段，别的控制器可能改过它。Regular = 老老实实按线切，
        // 不要在玩家画出闭合圈时改成抠洞
        Sliceable2D.complexSliceType = Sliceable2D.SliceType.Regular;

        Slice2D result = gem.ComplexSlice(slice);

        return AdoptResult(result, trail[0], trail[trail.Count - 1]);
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
        if (finished)
        {
            return;
        }

        finished = true;
        // 收刀要排在派发 Completed 之前：UI 拿到回调就会弹完成/失败窗，
        // 这时候刀子必须已经停下来了
        ParkTool();

        GemCutResult result = new GemCutResult
        {
            edgeCount = edges.Count,
            edgesCut = DoneCount(),
            freeCuts = freeCuts,
            targetDamage = targetDamage,
            failed = failed,

            // 直接复用最后一刀算出来的结果：
            // 评分口径 = 交并比 IoU = |宝石∩轮廓| / |宝石∪轮廓|，
            // 它同时惩罚两种错误——切进轮廓里（分子变小）和轮廓外没切干净（分母变大）。
            // 切进轮廓超过阈值的在 EvaluateAfterCut 里已经被压成 0 分。
            shapeScore = currentShape,
            score = currentScore
        };

        // 胜负口径统一在这里判，UI 直接用 result.win，不要各自再拿分数比一遍
        result.win = !failed && result.score >= winScore;

        Completed?.Invoke(result);
    }

    // ---------------- 表现 ----------------

    /// <summary>
    /// 只驱动刀子上那条 LineRenderer：拖拽时显示并喂坐标，抬手后隐藏。
    /// 外观（材质 / 线宽 / 颜色 / 排序）全部由那个组件自己决定，这里一概不碰。
    /// </summary>
    private void UpdateCutTrailLine()
    {
        if (cutTrailLine == null)
        {
            return;
        }

        if (!dragging || trail.Count < 2)
        {
            cutTrailLine.enabled = false;
            return;
        }

        // 喂进去的是世界坐标。这条线挂在刀子下面，而刀子在跟着鼠标跑，
        // 不强制 useWorldSpace 的话整条线会跟着光标一起漂。
        cutTrailLine.useWorldSpace = true;
        cutTrailLine.enabled = true;

        if (cutMode == GemCutMode.Complex && alignedEdge < 0)
        {
            // 自由轨迹：画玩家实际拖出来的整条折线
            cutTrailLine.positionCount = trail.Count;
            for (int i = 0; i < trail.Count; i++)
            {
                cutTrailLine.SetPosition(i, trail[i]);
            }
        }
        else
        {
            // 对准了目标边、或者 Split 模式：抬手后真正会切的是一条直线，就画直线
            cutTrailLine.positionCount = 2;
            cutTrailLine.SetPosition(0, dragStart);
            cutTrailLine.SetPosition(1, dragEnd);
        }
    }
}
