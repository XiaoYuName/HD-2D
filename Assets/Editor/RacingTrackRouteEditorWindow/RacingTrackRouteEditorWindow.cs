using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 赛道线路编辑器：策划在这里拖控制点画赛道，实时看到闭合中心线、曲率曲线和小地图预览。
///
/// 之所以不做成 Scene 里的组件而是独立窗口：赛道是纯 2D 俯视数据，不属于任何场景，
/// 放窗口里可以自带网格吸附和适配视图，也不会因为忘了保存场景而丢配置。
///
/// 操作：
///   拖拽控制点        移动（开了吸附会对齐网格）
///   Ctrl + 左键点线   在最近的线段上插入控制点
///   右键点控制点      删除（少于 3 个点时不允许）
///   中键 / Alt 拖拽   平移视图
///   滚轮              缩放
/// </summary>
public class RacingTrackRouteEditorWindow : EditorWindow
{
    const float HandleRadius = 6f;
    const float PickRadius = 10f;

    [SerializeField] RacingTrackRoute route;
    [SerializeField] bool snapEnabled = true;
    [SerializeField] float snapSize = 5f;
    [SerializeField] bool showCurvature = true;

    Vector2 _focus;          // 视图中心对应的世界坐标（米）
    float _zoom = 4f;        // 像素/米
    int _dragIndex = -1;
    Rect _canvas;
    RacingTrackMinimapStyle _previewStyle = new RacingTrackMinimapStyle();
    Texture2D _preview;

    [MenuItem("Tools/RacingCar/赛道线路编辑器")]
    static void Open() => GetWindow<RacingTrackRouteEditorWindow>("赛道线路").minSize = new Vector2(720f, 460f);

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
        float sideWidth = 220f;
        _canvas = new Rect(body.x, body.y, body.width - sideWidth, body.height);
        var side = new Rect(_canvas.xMax, body.y, sideWidth, body.height);

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
            route = (RacingTrackRoute)EditorGUILayout.ObjectField(route, typeof(RacingTrackRoute), false, GUILayout.Width(220f));
            if(EditorGUI.EndChangeCheck())
            {
                ReleasePreview();
                Frame();
            }

            snapEnabled = GUILayout.Toggle(snapEnabled, "吸附网格", EditorStyles.toolbarButton, GUILayout.Width(70f));
            snapSize = EditorGUILayout.FloatField(snapSize, EditorStyles.toolbarTextField, GUILayout.Width(40f));
            showCurvature = GUILayout.Toggle(showCurvature, "曲率", EditorStyles.toolbarButton, GUILayout.Width(44f));

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
            "拖拽 = 移动控制点\nCtrl+左键 = 在线上插点\n右键 = 删点\n中键/Alt = 平移，滚轮 = 缩放",
            MessageType.None);

        GUILayout.EndArea();
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
        {
            Vector2 a = W2G(new Vector2(x, min.y), local);
            Vector2 b = W2G(new Vector2(x, max.y), local);
            Handles.DrawLine(a, b);
        }
        for(float y = Mathf.Ceil(min.y / snapSize) * snapSize; y <= max.y; y += snapSize)
        {
            Vector2 a = W2G(new Vector2(min.x, y), local);
            Vector2 b = W2G(new Vector2(max.x, y), local);
            Handles.DrawLine(a, b);
        }

        // 原点十字
        Handles.color = new Color(1f, 1f, 1f, 0.18f);
        Handles.DrawLine(W2G(new Vector2(min.x, 0f), local), W2G(new Vector2(max.x, 0f), local));
        Handles.DrawLine(W2G(new Vector2(0f, min.y), local), W2G(new Vector2(0f, max.y), local));
        Handles.EndGUI();
    }

    void DrawCenterline(Rect local)
    {
        var pts = route.Points;
        int n = pts.Count;
        var gui = new Vector3[n + 1];
        for(int i = 0; i < n; i++)
            gui[i] = W2G(pts[i], local);
        gui[n] = gui[0];

        Handles.BeginGUI();
        Handles.color = new Color(0.90f, 0.45f, 0.42f);
        Handles.DrawAAPolyLine(4f, gui);
        Handles.EndGUI();
    }

    /// <summary>把曲率画成中心线两侧的色带：鼓出去的一侧就是转向方向，宽度即曲率大小。手拖出的毛刺一眼可见。</summary>
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
            // 半径 20m 的弯画到 8m 长，比例够看清又不会糊成一片
            Vector2 tip = pts[i] + normal * Mathf.Clamp(k * 160f, -12f, 12f);

            Handles.color = k > 0f ? new Color(0.4f, 0.8f, 1f, 0.5f) : new Color(1f, 0.8f, 0.3f, 0.5f);
            Handles.DrawLine(W2G(pts[i], local), W2G(tip, local));
        }
        Handles.EndGUI();
    }

    void DrawStartMarker(Rect local)
    {
        Vector2 p = route.PositionAt(0f);
        float heading = route.HeadingAt(0f);
        var normal = new Vector2(-Mathf.Sin(heading), Mathf.Cos(heading));

        Handles.BeginGUI();
        Handles.color = new Color(0.45f, 0.90f, 0.88f);
        Handles.DrawAAPolyLine(5f, W2G(p - normal * 6f, local), W2G(p + normal * 6f, local));
        Handles.EndGUI();

        var label = new Rect(W2G(p, local) + new Vector2(8f, -20f), new Vector2(60f, 16f));
        GUI.Label(label, "起点", EditorStyles.miniLabel);
    }

    void DrawControlPoints(Rect local)
    {
        var cps = route.ControlPoints;
        for(int i = 0; i < cps.Count; i++)
        {
            Vector2 g = W2G(cps[i], local);
            var r = new Rect(g.x - HandleRadius, g.y - HandleRadius, HandleRadius * 2f, HandleRadius * 2f);
            EditorGUI.DrawRect(r, i == _dragIndex ? Color.white : new Color(1f, 0.92f, 0.4f));
        }
    }
    #endregion

    #region 交互
    void HandleInput()
    {
        Event e = Event.current;
        if(!_canvas.Contains(e.mousePosition) && _dragIndex < 0)
            return;

        var local = new Rect(0f, 0f, _canvas.width, _canvas.height);
        Vector2 mouseLocal = e.mousePosition - new Vector2(_canvas.x, _canvas.y);

        switch(e.type)
        {
            case EventType.ScrollWheel:
            {
                // 以光标为锚点缩放，视线不会跑掉
                Vector2 before = G2W(mouseLocal, local);
                _zoom = Mathf.Clamp(_zoom * (e.delta.y > 0f ? 0.9f : 1.1f), 0.2f, 60f);
                Vector2 after = G2W(mouseLocal, local);
                _focus += before - after;
                e.Use();
                Repaint();
                break;
            }

            case EventType.MouseDown:
            {
                if(e.button == 2 || e.alt)
                    break;

                int hit = PickPoint(mouseLocal, local);

                if(e.button == 1)
                {
                    if(hit >= 0 && route.ControlPoints.Count > 3)
                    {
                        Undo.RecordObject(route, "删除赛道控制点");
                        route.ControlPoints.RemoveAt(hit);
                        Rebake();
                        e.Use();
                    }
                    break;
                }

                if(e.button != 0)
                    break;

                if(e.control || e.command)
                {
                    InsertPointNear(G2W(mouseLocal, local));
                    e.Use();
                    break;
                }

                if(hit >= 0)
                {
                    _dragIndex = hit;
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    e.Use();
                }
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

                if(_dragIndex >= 0)
                {
                    Undo.RecordObject(route, "移动赛道控制点");
                    route.ControlPoints[_dragIndex] = Snap(G2W(mouseLocal, local));
                    Rebake();
                    e.Use();
                }
                break;
            }

            case EventType.MouseUp:
                if(_dragIndex >= 0)
                {
                    _dragIndex = -1;
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;
        }
    }

    int PickPoint(Vector2 mouseLocal, Rect local)
    {
        var cps = route.ControlPoints;
        for(int i = 0; i < cps.Count; i++)
            if(Vector2.Distance(W2G(cps[i], local), mouseLocal) <= PickRadius)
                return i;
        return -1;
    }

    /// <summary>在离点击处最近的那条「控制点连线」上插入新点，插在两个端点之间，不会打乱走向。</summary>
    void InsertPointNear(Vector2 world)
    {
        List<Vector2> cps = route.ControlPoints;
        int n = cps.Count;
        int best = 0;
        float bestDist = float.MaxValue;

        for(int i = 0; i < n; i++)
        {
            Vector2 a = cps[i];
            Vector2 b = cps[(i + 1) % n];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(world - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            float d = Vector2.Distance(world, a + ab * t);
            if(d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        Undo.RecordObject(route, "插入赛道控制点");
        cps.Insert(best + 1, Snap(world));
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
        float w = Mathf.Max(b.width, 1f);
        float h = Mathf.Max(b.height, 1f);
        _zoom = Mathf.Clamp(Mathf.Min(600f / w, 380f / h), 0.2f, 60f);
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
