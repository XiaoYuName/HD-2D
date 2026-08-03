using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrackPoint = RacingTrackRoute.TrackPoint;

/// <summary>
/// 赛道线路编辑器：策划在这里拖控制点画赛道，实时看到闭合中心线、曲率分布和小地图预览。
///
/// 之所以不做成 Scene 里的组件而是独立窗口：赛道是纯 2D 俯视数据，不属于任何场景，
/// 放窗口里可以自带网格吸附和适配视图，也不会因为忘了保存场景而丢配置。
///
/// 曲线用三次贝塞尔，每个点带两根切线手柄，和 Unity 曲线编辑器是同一套操作。
/// 「自动」模式下切线由相邻点现算（等价于 Catmull-Rom），拖过手柄就自动转成手动。
///
/// 操作：
///   左键点/拖 控制点     选中并移动（开了吸附会对齐网格）
///   左键点 曲线          选中该段，两端手柄一起显示，可直接调这一段的弯曲度
///   拖 圆形手柄          调切线；默认两侧镜像联动，勾「断开切线」后可各调各的
///   双击 曲线            在点击处插入一个控制点
///   右键 控制点          删除
///   Delete / Backspace   删除选中的控制点
///   中键 / Alt 拖拽      平移视图，滚轮缩放
/// </summary>
public class RacingTrackRouteEditorWindow : EditorWindow
{
    const float HandleRadius = 6f;
    const float TangentRadius = 5f;
    const float PickRadius = 11f;

    enum DragKind { None, Point, TangentIn, TangentOut }

    [SerializeField] RacingTrackRoute route;
    [SerializeField] bool snapEnabled = true;
    [SerializeField] float snapSize = 5f;
    [SerializeField] bool showCurvature = true;

    Vector2 _focus;          // 视图中心对应的世界坐标（米）
    float _zoom = 4f;        // 像素/米
    Rect _canvas;

    int _selected = -1;      // 选中的控制点
    int _selectedSeg = -1;   // 选中的线段（起点索引）；选中线段时两端手柄都显示
    DragKind _drag = DragKind.None;
    int _dragIndex = -1;

    RacingTrackMinimapStyle _previewStyle = new RacingTrackMinimapStyle();
    Texture2D _preview;

    [MenuItem("Tools/RacingCar/赛道线路编辑器")]
    static void Open() => GetWindow<RacingTrackRouteEditorWindow>("赛道线路").minSize = new Vector2(760f, 500f);

    void OnEnable()
    {
        if(route == null && Selection.activeObject is RacingTrackRoute r)
            route = r;
        Frame();
    }

    void OnDisable() => ReleasePreview();

    void OnGUI()
    {
        DrawToolbar();

        if(route == null)
        {
            EditorGUILayout.HelpBox("先选一个 RacingTrackRoute 资产。\n没有的话：Project 右键 → Create → 小游戏 → RacingTrackRoute", MessageType.Info);
            return;
        }

        Rect body = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f);
        const float SideWidth = 250f;
        _canvas = new Rect(body.x, body.y, body.width - SideWidth, body.height);
        var side = new Rect(_canvas.xMax, body.y, SideWidth, body.height);

        DrawCanvas();
        DrawSidePanel(side);
        HandleInput();
    }

    #region 工具栏 / 侧栏
    void DrawToolbar()
    {
        using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            route = (RacingTrackRoute)EditorGUILayout.ObjectField(route, typeof(RacingTrackRoute), false, GUILayout.Width(200f));
            if(EditorGUI.EndChangeCheck())
            {
                ReleasePreview();
                _selected = _selectedSeg = -1;
                Frame();
            }

            using(new EditorGUI.DisabledScope(route == null))
            {
                if(GUILayout.Button("加点", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    InsertAfterSelected();

                using(new EditorGUI.DisabledScope(_selected < 0))
                {
                    if(GUILayout.Button("删点", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                        DeletePoint(_selected);
                }
            }

            if(route != null)
            {
                EditorGUI.BeginChangeCheck();
                bool cl = GUILayout.Toggle(route.IsClosed, "首尾相连", EditorStyles.toolbarButton, GUILayout.Width(64f));
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(route, "切换赛道闭合");
                    route.IsClosed = cl;
                    _selectedSeg = -1;
                    Rebake();
                }
            }

            snapEnabled = GUILayout.Toggle(snapEnabled, "吸附网格", EditorStyles.toolbarButton, GUILayout.Width(64f));
            snapSize = EditorGUILayout.FloatField(snapSize, EditorStyles.toolbarTextField, GUILayout.Width(36f));
            showCurvature = GUILayout.Toggle(showCurvature, "曲率", EditorStyles.toolbarButton, GUILayout.Width(40f));

            if(GUILayout.Button("适配视图", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                Frame();

            GUILayout.FlexibleSpace();

            if(route != null && GUILayout.Button("重新烘焙", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                Rebake();
        }
    }

    void DrawSidePanel(Rect area)
    {
        GUILayout.BeginArea(area, EditorStyles.helpBox);

        EditorGUILayout.LabelField("赛道信息", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("一圈长度", route.IsBaked ? $"{route.TotalLength:F1} m" : "未烘焙");
        EditorGUILayout.LabelField("控制点", route.ControlPoints.Count.ToString());
        EditorGUILayout.LabelField("包围盒", route.IsBaked ? $"{route.Bounds.width:F0} × {route.Bounds.height:F0} m" : "-");

        EditorGUILayout.Space();
        DrawSelectedPointPanel();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("小地图预览", EditorStyles.boldLabel);

        if(_preview == null && route.IsBaked)
            _preview = RacingTrackMinimap.Bake(route, _previewStyle);

        if(_preview != null)
        {
            // 按整数倍放大，保持像素硬边，跟运行时看到的一致
            float scale = Mathf.Max(1f, Mathf.Floor((area.width - 20f) / _preview.width));
            Rect r = GUILayoutUtility.GetRect(_preview.width * scale, _preview.height * scale);
            EditorGUI.DrawRect(r, Color.black);
            GUI.DrawTexture(r, _preview, ScaleMode.ScaleToFit, true);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "点/拖 方块 = 选中并移动\n点 曲线 = 选中该段\n拖 圆点 = 调这一段的弯曲度\n双击曲线 = 插点，右键方块 = 删点\n中键/Alt = 平移，滚轮 = 缩放",
            MessageType.None);

        GUILayout.EndArea();
    }

    void DrawSelectedPointPanel()
    {
        var pts = route.ControlPoints;

        if(_selected < 0 || _selected >= pts.Count)
        {
            EditorGUILayout.LabelField("当前选中", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_selectedSeg >= 0 ? $"线段 {_selectedSeg} → {(_selectedSeg + 1) % pts.Count}" : "无");
            return;
        }

        TrackPoint p = pts[_selected];
        EditorGUILayout.LabelField($"控制点 {_selected}", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        Vector2 pos = EditorGUILayout.Vector2Field("坐标", p.Position);
        bool auto = EditorGUILayout.Toggle("自动切线", p.Auto);
        bool broken = auto ? p.Broken : EditorGUILayout.Toggle("断开两侧", p.Broken);
        if(EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(route, "编辑赛道控制点");

            // 自动 -> 手动时把当前实际切线固化下来，形状不跳变
            if(p.Auto && !auto)
            {
                route.ResolveTangents(_selected, out Vector2 inT, out Vector2 outT);
                p.InTangent = inT;
                p.OutTangent = outT;
            }

            p.Position = pos;
            p.Auto = auto;
            p.Broken = broken;
            Rebake();
        }

        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("在其后插点"))
                InsertAfterSelected();
            if(GUILayout.Button("删除本点"))
                DeletePoint(_selected);
        }
    }
    #endregion

    #region 画布
    void DrawCanvas()
    {
        EditorGUI.DrawRect(_canvas, new Color(0.13f, 0.13f, 0.15f));

        GUI.BeginClip(_canvas);
        var local = new Rect(0f, 0f, _canvas.width, _canvas.height);

        DrawGrid(local);

        if(route.IsBaked)
        {
            DrawCenterline(local);
            if(showCurvature)
                DrawCurvatureStrip(local);
            DrawStartMarker(local);
        }

        DrawTangents(local);
        DrawControlPoints(local);

        GUI.EndClip();
    }

    void DrawGrid(Rect local)
    {
        if(snapSize <= 0f || snapSize * _zoom < 4f)
            return;

        Handles.BeginGUI();
        Vector2 min = G2W(new Vector2(0f, local.height), local);
        Vector2 max = G2W(new Vector2(local.width, 0f), local);

        Handles.color = new Color(1f, 1f, 1f, 0.06f);
        for(float x = Mathf.Ceil(min.x / snapSize) * snapSize; x <= max.x; x += snapSize)
            Handles.DrawLine(W2G(new Vector2(x, min.y), local), W2G(new Vector2(x, max.y), local));
        for(float y = Mathf.Ceil(min.y / snapSize) * snapSize; y <= max.y; y += snapSize)
            Handles.DrawLine(W2G(new Vector2(min.x, y), local), W2G(new Vector2(max.x, y), local));

        Handles.color = new Color(1f, 1f, 1f, 0.18f);
        Handles.DrawLine(W2G(new Vector2(min.x, 0f), local), W2G(new Vector2(max.x, 0f), local));
        Handles.DrawLine(W2G(new Vector2(0f, min.y), local), W2G(new Vector2(0f, max.y), local));
        Handles.EndGUI();
    }

    void DrawCenterline(Rect local)
    {
        var pts = route.Points;
        int n = pts.Count;
        // 开放赛道不能把末点连回首点，否则画面上会多出一条实际不存在的边
        int count = route.IsClosed ? n + 1 : n;
        var gui = new Vector3[count];
        for(int i = 0; i < n; i++)
            gui[i] = W2G(pts[i], local);
        if(route.IsClosed)
            gui[n] = gui[0];

        Handles.BeginGUI();
        Handles.color = new Color(0.90f, 0.45f, 0.42f);
        Handles.DrawAAPolyLine(4f, gui);

        // 选中的线段整段高亮，一眼看出在调哪一段
        if(_selectedSeg >= 0)
        {
            var cps = route.ControlPoints;
            int cn = cps.Count;
            if(_selectedSeg < route.SegmentCount)
            {
                int next = (_selectedSeg + 1) % cn;
                route.ResolveTangents(_selectedSeg, out _, out Vector2 outT);
                route.ResolveTangents(next, out Vector2 inT, out _);
                Vector2 b0 = cps[_selectedSeg].Position;
                Vector2 b3 = cps[next].Position;

                const int Steps = 32;
                var seg = new Vector3[Steps + 1];
                for(int i = 0; i <= Steps; i++)
                    seg[i] = W2G(RacingTrackRoute.Bezier(b0, b0 + outT, b3 + inT, b3, (float)i / Steps), local);

                Handles.color = new Color(1f, 0.85f, 0.3f);
                Handles.DrawAAPolyLine(6f, seg);
            }
        }
        Handles.EndGUI();
    }

    /// <summary>把曲率画成中心线一侧的须：鼓出去的一侧就是转向方向，长度即曲率大小。手拖出的毛刺一眼可见。</summary>
    void DrawCurvatureStrip(Rect local)
    {
        var pts = route.Points;
        int n = pts.Count;
        float step = route.TotalLength / n;

        Handles.BeginGUI();
        for(int i = 0; i < n; i += 2)
        {
            float k = route.CurvatureAt(i * step);
            if(Mathf.Abs(k) < 1e-4f)
                continue;

            float heading = route.HeadingAt(i * step);
            var normal = new Vector2(-Mathf.Sin(heading), Mathf.Cos(heading));
            Vector2 tip = pts[i] + normal * Mathf.Clamp(k * 160f, -12f, 12f);

            Handles.color = k > 0f ? new Color(0.4f, 0.8f, 1f, 0.5f) : new Color(1f, 0.8f, 0.3f, 0.5f);
            Handles.DrawLine(W2G(pts[i], local), W2G(tip, local));
        }
        Handles.EndGUI();
    }

    void DrawStartMarker(Rect local)
    {
        DrawMarker(local, 0f, new Color(0.45f, 0.90f, 0.88f), route.IsClosed ? "起点/终点" : "起点");

        // 开放赛道的终点不在起点上，单独标出来
        if(!route.IsClosed)
            DrawMarker(local, route.TotalLength, new Color(1f, 0.85f, 0.35f), "终点");
    }

    void DrawMarker(Rect local, float s, Color color, string label)
    {
        Vector2 p = route.PositionAt(s);
        float heading = route.HeadingAt(s);
        var normal = new Vector2(-Mathf.Sin(heading), Mathf.Cos(heading));

        Handles.BeginGUI();
        Handles.color = color;
        Handles.DrawAAPolyLine(5f, W2G(p - normal * 6f, local), W2G(p + normal * 6f, local));
        Handles.EndGUI();

        GUI.Label(new Rect(W2G(p, local) + new Vector2(8f, -20f), new Vector2(80f, 16f)), label, EditorStyles.miniLabel);
    }

    /// <summary>只画「当前关注的点」的切线手柄，全部点都画会糊成一团。</summary>
    void DrawTangents(Rect local)
    {
        var pts = route.ControlPoints;
        int n = pts.Count;
        if(n == 0)
            return;

        Handles.BeginGUI();
        foreach(int i in TangentTargets())
        {
            route.ResolveTangents(i, out Vector2 inT, out Vector2 outT);
            Vector2 pos = pts[i].Position;
            Vector2 g = W2G(pos, local);
            Vector2 gi = W2G(pos + inT, local);
            Vector2 go = W2G(pos + outT, local);

            Handles.color = pts[i].Auto ? new Color(0.5f, 0.6f, 0.7f, 0.7f) : new Color(0.4f, 0.9f, 1f);
            Handles.DrawAAPolyLine(2f, g, gi);
            Handles.DrawAAPolyLine(2f, g, go);

            Color c = pts[i].Auto ? new Color(0.55f, 0.62f, 0.7f) : new Color(0.4f, 0.9f, 1f);
            DrawDisc(gi, TangentRadius, c);
            DrawDisc(go, TangentRadius, c);
        }
        Handles.EndGUI();
    }

    /// <summary>要显示切线的点：选中点本身，或选中线段的两个端点。</summary>
    IEnumerable<int> TangentTargets()
    {
        int n = route.ControlPoints.Count;
        if(_selected >= 0 && _selected < n)
        {
            yield return _selected;
        }
        else if(_selectedSeg >= 0 && _selectedSeg < n)
        {
            yield return _selectedSeg;
            yield return (_selectedSeg + 1) % n;
        }
    }

    static void DrawDisc(Vector2 center, float radius, Color color)
    {
        Handles.color = color;
        Handles.DrawSolidDisc(center, Vector3.forward, radius);
    }

    void DrawControlPoints(Rect local)
    {
        var pts = route.ControlPoints;
        for(int i = 0; i < pts.Count; i++)
        {
            Vector2 g = W2G(pts[i].Position, local);
            var r = new Rect(g.x - HandleRadius, g.y - HandleRadius, HandleRadius * 2f, HandleRadius * 2f);
            EditorGUI.DrawRect(r, i == _selected ? Color.white : new Color(1f, 0.92f, 0.4f));
        }
    }
    #endregion

    #region 交互
    void HandleInput()
    {
        Event e = Event.current;
        if(!_canvas.Contains(e.mousePosition) && _drag == DragKind.None)
            return;

        var local = new Rect(0f, 0f, _canvas.width, _canvas.height);
        Vector2 mouse = e.mousePosition - new Vector2(_canvas.x, _canvas.y);

        switch(e.type)
        {
            case EventType.ScrollWheel:
            {
                // 以光标为锚点缩放，视线不会跑掉
                Vector2 before = G2W(mouse, local);
                _zoom = Mathf.Clamp(_zoom * (e.delta.y > 0f ? 0.9f : 1.1f), 0.2f, 60f);
                _focus += before - G2W(mouse, local);
                e.Use();
                Repaint();
                break;
            }

            case EventType.KeyDown:
                if((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && _selected >= 0)
                {
                    DeletePoint(_selected);
                    e.Use();
                }
                break;

            case EventType.MouseDown:
            {
                if(e.button == 2 || e.alt)
                    break;

                if(e.button == 1)
                {
                    int hit = PickPoint(mouse, local);
                    if(hit >= 0)
                    {
                        DeletePoint(hit);
                        e.Use();
                    }
                    break;
                }

                if(e.button != 0)
                    break;

                // 手柄优先于点：它们经常叠在一起，先判手柄才拖得动
                if(PickTangent(mouse, local, out int tIdx, out bool isIn))
                {
                    _drag = isIn ? DragKind.TangentIn : DragKind.TangentOut;
                    _dragIndex = tIdx;
                    e.Use();
                    break;
                }

                int p = PickPoint(mouse, local);
                if(p >= 0)
                {
                    _selected = p;
                    _selectedSeg = -1;
                    _drag = DragKind.Point;
                    _dragIndex = p;
                    e.Use();
                    Repaint();
                    break;
                }

                // 点到曲线：选中该段；双击则在点击处插点
                int seg = PickSegment(mouse, local, out float segT);
                if(seg >= 0)
                {
                    if(e.clickCount >= 2)
                        InsertOnSegment(seg, segT);
                    else
                    {
                        _selectedSeg = seg;
                        _selected = -1;
                    }
                    e.Use();
                    Repaint();
                    break;
                }

                _selected = _selectedSeg = -1;
                Repaint();
                break;
            }

            case EventType.MouseDrag:
            {
                if(e.button == 2 || e.alt)
                {
                    _focus -= new Vector2(e.delta.x, -e.delta.y) / _zoom;
                    e.Use();
                    Repaint();
                    break;
                }

                if(_drag == DragKind.Point)
                {
                    Undo.RecordObject(route, "移动赛道控制点");
                    route.ControlPoints[_dragIndex].Position = Snap(G2W(mouse, local));
                    Rebake();
                    e.Use();
                }
                else if(_drag != DragKind.None)
                {
                    DragTangent(_dragIndex, _drag == DragKind.TangentIn, G2W(mouse, local));
                    e.Use();
                }
                break;
            }

            case EventType.MouseUp:
                if(_drag != DragKind.None)
                {
                    _drag = DragKind.None;
                    _dragIndex = -1;
                    e.Use();
                }
                break;
        }
    }

    void DragTangent(int index, bool isIn, Vector2 world)
    {
        var pts = route.ControlPoints;
        TrackPoint p = pts[index];

        Undo.RecordObject(route, "调整赛道切线");

        // 第一次拖手柄自动从「自动」切到「手动」，并保留当前形状作为起点
        if(p.Auto)
        {
            route.ResolveTangents(index, out Vector2 i0, out Vector2 o0);
            p.InTangent = i0;
            p.OutTangent = o0;
            p.Auto = false;
        }

        Vector2 t = world - p.Position;

        if(isIn)
        {
            p.InTangent = t;
            if(!p.Broken)
                p.OutTangent = -t;   // 镜像联动，保证曲线在该点处光滑（G1 连续）
        }
        else
        {
            p.OutTangent = t;
            if(!p.Broken)
                p.InTangent = -t;
        }

        Rebake();
    }

    int PickPoint(Vector2 mouse, Rect local)
    {
        var pts = route.ControlPoints;
        for(int i = 0; i < pts.Count; i++)
            if(Vector2.Distance(W2G(pts[i].Position, local), mouse) <= PickRadius)
                return i;
        return -1;
    }

    bool PickTangent(Vector2 mouse, Rect local, out int index, out bool isIn)
    {
        index = -1;
        isIn = false;

        var pts = route.ControlPoints;
        foreach(int i in TangentTargets())
        {
            route.ResolveTangents(i, out Vector2 inT, out Vector2 outT);
            Vector2 pos = pts[i].Position;

            if(Vector2.Distance(W2G(pos + inT, local), mouse) <= PickRadius)
            {
                index = i;
                isIn = true;
                return true;
            }
            if(Vector2.Distance(W2G(pos + outT, local), mouse) <= PickRadius)
            {
                index = i;
                isIn = false;
                return true;
            }
        }
        return false;
    }

    /// <summary>点中了哪一段曲线，顺带给出段内参数 t。按实际贝塞尔取样判距离，不是按控制点连线。</summary>
    int PickSegment(Vector2 mouse, Rect local, out float t)
    {
        t = 0f;
        var pts = route.ControlPoints;
        int n = pts.Count;
        if(n < 2)
            return -1;

        const int Steps = 24;
        int best = -1;
        float bestDist = PickRadius * 1.6f;

        for(int i = 0; i < route.SegmentCount; i++)
        {
            int next = (i + 1) % n;
            route.ResolveTangents(i, out _, out Vector2 outT);
            route.ResolveTangents(next, out Vector2 inT, out _);
            Vector2 b0 = pts[i].Position, b3 = pts[next].Position;
            Vector2 b1 = b0 + outT, b2 = b3 + inT;

            for(int j = 0; j <= Steps; j++)
            {
                float u = (float)j / Steps;
                float d = Vector2.Distance(W2G(RacingTrackRoute.Bezier(b0, b1, b2, b3, u), local), mouse);
                if(d < bestDist)
                {
                    bestDist = d;
                    best = i;
                    t = u;
                }
            }
        }
        return best;
    }

    void InsertAfterSelected()
    {
        var pts = route.ControlPoints;
        if(pts.Count == 0)
        {
            Undo.RecordObject(route, "插入赛道控制点");
            pts.Add(new TrackPoint(_focus));
            Rebake();
            return;
        }

        int at = _selected >= 0 ? _selected : (_selectedSeg >= 0 ? _selectedSeg : pts.Count - 1);
        InsertOnSegment(at, 0.5f);
    }

    /// <summary>在第 seg 段的 t 处插点。新点用自动切线，插完形状基本不变。</summary>
    void InsertOnSegment(int seg, float t)
    {
        var pts = route.ControlPoints;
        int n = pts.Count;
        int next = (seg + 1) % n;

        route.ResolveTangents(seg, out _, out Vector2 outT);
        route.ResolveTangents(next, out Vector2 inT, out _);
        Vector2 b0 = pts[seg].Position, b3 = pts[next].Position;
        Vector2 world = RacingTrackRoute.Bezier(b0, b0 + outT, b3 + inT, b3, t);

        Undo.RecordObject(route, "插入赛道控制点");
        pts.Insert(seg + 1, new TrackPoint(Snap(world)));
        _selected = seg + 1;
        _selectedSeg = -1;
        Rebake();
    }

    void DeletePoint(int index)
    {
        var pts = route.ControlPoints;
        if(index < 0 || index >= pts.Count)
            return;

        if(pts.Count <= route.MinPointCount)
        {
            EditorUtility.DisplayDialog("删不了",
                route.IsClosed ? "闭合曲线至少要保留 3 个控制点。" : "开放曲线至少要保留 2 个控制点。", "知道了");
            return;
        }

        Undo.RecordObject(route, "删除赛道控制点");
        pts.RemoveAt(index);
        _selected = -1;
        _selectedSeg = -1;
        Rebake();
    }

    Vector2 Snap(Vector2 world)
    {
        if(!snapEnabled || snapSize <= 0f)
            return world;
        return new Vector2(Mathf.Round(world.x / snapSize) * snapSize, Mathf.Round(world.y / snapSize) * snapSize);
    }

    void Rebake()
    {
        route.Bake();
        EditorUtility.SetDirty(route);
        ReleasePreview();
        Repaint();
    }

    void Frame()
    {
        if(route == null || !route.IsBaked)
            return;

        Rect b = route.Bounds;
        _focus = b.center;
        _zoom = Mathf.Clamp(Mathf.Min(600f / Mathf.Max(b.width, 1f), 380f / Mathf.Max(b.height, 1f)), 0.2f, 60f);
        Repaint();
    }

    void ReleasePreview()
    {
        if(_preview != null)
        {
            DestroyImmediate(_preview);
            _preview = null;
        }
    }
    #endregion

    #region 坐标变换
    Vector2 W2G(Vector2 w, Rect local) => new Vector2(
        local.width * 0.5f + (w.x - _focus.x) * _zoom,
        local.height * 0.5f - (w.y - _focus.y) * _zoom);

    Vector2 G2W(Vector2 g, Rect local) => new Vector2(
        _focus.x + (g.x - local.width * 0.5f) / _zoom,
        _focus.y - (g.y - local.height * 0.5f) / _zoom);
    #endregion
}
