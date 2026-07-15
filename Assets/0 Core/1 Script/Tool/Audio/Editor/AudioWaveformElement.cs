#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 波形显示与选区交互控件（AU 风格）：空白处拖拽创建选区；选区创建后固定不动，拖动左右边缘把手调整范围、
/// 按住选区内部整体平移、选区内单击定位光标不清除选区、选区外单击清除选区并定位光标。
/// 滚轮以光标为中心缩放，Shift+滚轮平移。坐标单位为“帧”（每声道采样数），多声道分行显示。
/// </summary>
public class AudioWaveformElement : VisualElement
{
    static readonly Color WaveColor = new(0.25f, 0.85f, 0.45f);
    static readonly Color CenterColor = new(1f, 1f, 1f, 0.12f);
    static readonly Color SelFill = new(0.35f, 0.55f, 0.95f, 0.22f);
    static readonly Color SelEdge = new(0.5f, 0.7f, 1f, 0.9f);
    static readonly Color PlayheadColor = new(1f, 0.85f, 0.3f, 0.95f);

    float[] samples;
    int channels = 1;

    public int Frequency { get; private set; } = 44100;
    public long Frames { get; private set; }
    public long ViewStart { get; private set; }
    public long ViewEnd { get; private set; } = 1;
    public long SelStart { get; private set; } = -1;
    public long SelEnd { get; private set; } = -1;
    public long Playhead { get; private set; }
    public bool HasSelection => SelStart >= 0 && SelEnd > SelStart;

    public event Action SelectionChanged;
    public event Action ViewChanged;

    const float EdgePx = 10f;   // 边缘把手的抓取范围（像素）

    enum DragMode { None, NewSel, EdgeStart, EdgeEnd, MoveSel }
    DragMode drag;
    long dragAnchor;
    long grabOffset;            // MoveSel：按下点相对选区起点的帧偏移
    Vector2 downPos;
    bool dragMoved;

    static StyleCursor? cursorResize, cursorMove;

    public AudioWaveformElement()
    {
        focusable = true;
        style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
        style.flexGrow = 1f;
        style.minHeight = 160f;
        generateVisualContent += Paint;
        RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        RegisterCallback<PointerDownEvent>(OnDown);
        RegisterCallback<PointerMoveEvent>(OnMove);
        RegisterCallback<PointerUpEvent>(OnUp);
        RegisterCallback<PointerLeaveEvent>(_ => { if (drag == DragMode.None) style.cursor = new StyleCursor(StyleKeyword.Null); });
        RegisterCallback<WheelEvent>(OnWheel);
    }

    public void SetData(float[] s, int ch, int freq)
    {
        // 布局不变（如仅调整音量）时保留选区/缩放/光标，只刷新波形
        bool sameLayout = s != null && samples != null && ch == channels && s.Length == samples.Length;
        samples = s;
        channels = Mathf.Max(1, ch);
        Frequency = freq;
        Frames = s == null ? 0 : s.Length / channels;
        if (!sameLayout)
        {
            ViewStart = 0;
            ViewEnd = Math.Max(1, Frames);
            SelStart = SelEnd = -1;
            Playhead = 0;
        }
        MarkDirtyRepaint();
        ViewChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void SetView(long start, long end)
    {
        if (Frames == 0) return;
        long span = Math.Clamp(end - start, 32, Frames);
        start = Math.Clamp(start, 0, Frames - span);
        ViewStart = start;
        ViewEnd = start + span;
        MarkDirtyRepaint();
        ViewChanged?.Invoke();
    }

    public void SetSelection(long s, long e)
    {
        SelStart = Math.Clamp(s, 0, Frames);
        SelEnd = Math.Clamp(e, 0, Frames);
        MarkDirtyRepaint();
        SelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        SelStart = SelEnd = -1;
        MarkDirtyRepaint();
        SelectionChanged?.Invoke();
    }

    public void SetPlayhead(long f)
    {
        Playhead = Math.Clamp(f, 0, Frames);
        MarkDirtyRepaint();
    }

    float XOf(long f) => (float)((f - ViewStart) / (double)(ViewEnd - ViewStart)) * contentRect.width;

    long FrameAt(float x)
    {
        float w = Mathf.Max(1f, contentRect.width);
        long f = ViewStart + (long)((ViewEnd - ViewStart) * (double)(x / w));
        return Math.Clamp(f, 0, Frames);
    }

    // ---- 交互 ----

    /// <summary>按下/悬停位置对应的操作区域：边缘把手优先，其次选区内部，最后空白。</summary>
    DragMode ZoneAt(float x)
    {
        if (!HasSelection) return DragMode.NewSel;
        if (Mathf.Abs(x - XOf(SelStart)) <= EdgePx) return DragMode.EdgeStart;
        if (Mathf.Abs(x - XOf(SelEnd)) <= EdgePx) return DragMode.EdgeEnd;
        if (x > XOf(SelStart) && x < XOf(SelEnd)) return DragMode.MoveSel;
        return DragMode.NewSel;
    }

    void OnDown(PointerDownEvent e)
    {
        if (e.button != 0 || Frames == 0) return;
        Focus();
        downPos = e.localPosition;
        dragMoved = false;
        float x = e.localPosition.x;
        drag = ZoneAt(x);
        switch (drag)
        {
            case DragMode.EdgeStart: dragAnchor = SelEnd; break;
            case DragMode.EdgeEnd: dragAnchor = SelStart; break;
            case DragMode.MoveSel: grabOffset = FrameAt(x) - SelStart; break;
            default: dragAnchor = FrameAt(x); break;
        }
        this.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    void OnMove(PointerMoveEvent e)
    {
        if (drag == DragMode.None || !this.HasPointerCapture(e.pointerId))
        {
            UpdateHoverCursor(e.localPosition.x);
            return;
        }
        if (((Vector2)e.localPosition - downPos).sqrMagnitude > 9f) dragMoved = true;
        if (!dragMoved) return;
        long f = FrameAt(e.localPosition.x);
        if (drag == DragMode.MoveSel)
        {
            long len = SelEnd - SelStart;
            long start = Math.Clamp(f - grabOffset, 0, Frames - len);
            SetSelection(start, start + len);
        }
        else
        {
            SetSelection(Math.Min(dragAnchor, f), Math.Max(dragAnchor, f));
        }
    }

    void OnUp(PointerUpEvent e)
    {
        if (drag == DragMode.None) return;
        this.ReleasePointer(e.pointerId);
        if (!dragMoved)
        {
            // 单击：选区外清除选区并定位光标；选区内只定位光标，选区保持不动
            if (drag == DragMode.NewSel) ClearSelection();
            if (drag is DragMode.NewSel or DragMode.MoveSel) SetPlayhead(FrameAt(e.localPosition.x));
        }
        drag = DragMode.None;
        UpdateHoverCursor(e.localPosition.x);
    }

    // ---- 鼠标指针反馈（边缘=横向缩放，选区内=移动） ----

    void UpdateHoverCursor(float x)
    {
        if (Frames == 0 || !HasSelection) { style.cursor = new StyleCursor(StyleKeyword.Null); return; }
        var zone = ZoneAt(x);
        if (zone is DragMode.EdgeStart or DragMode.EdgeEnd)
            style.cursor = cursorResize ??= MakeCursor(UnityEditor.MouseCursor.ResizeHorizontal);
        else if (zone == DragMode.MoveSel)
            style.cursor = cursorMove ??= MakeCursor(UnityEditor.MouseCursor.MoveArrow);
        else
            style.cursor = new StyleCursor(StyleKeyword.Null);
    }

    // UIToolkit 没有公开的内置指针 API，这里反射设置 Cursor.defaultCursorId
    static StyleCursor MakeCursor(UnityEditor.MouseCursor id)
    {
        try
        {
            object boxed = new UnityEngine.UIElements.Cursor();
            typeof(UnityEngine.UIElements.Cursor)
                .GetProperty("defaultCursorId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(boxed, (int)id);
            return new StyleCursor((UnityEngine.UIElements.Cursor)boxed);
        }
        catch { return new StyleCursor(StyleKeyword.Null); }
    }

    void OnWheel(WheelEvent e)
    {
        if (Frames == 0) return;
        long span = ViewEnd - ViewStart;
        if (e.shiftKey)
        {
            long step = (long)(span * 0.15f) * (e.delta.y > 0 ? 1 : -1);
            SetView(ViewStart + step, ViewEnd + step);
        }
        else
        {
            double k = e.delta.y > 0 ? 1.3 : 1 / 1.3;
            long focus = FrameAt(e.localMousePosition.x);
            SetView(focus - (long)((focus - ViewStart) * k), focus + (long)((ViewEnd - focus) * k));
        }
        e.StopPropagation();
    }

    // ---- 绘制 ----

    void Paint(MeshGenerationContext mgc)
    {
        var r = contentRect;
        if (r.width < 2f || r.height < 2f || samples == null || Frames == 0) return;
        var p = mgc.painter2D;
        int w = Mathf.CeilToInt(r.width);
        long span = ViewEnd - ViewStart;

        // 选区底色
        if (HasSelection && SelEnd > ViewStart && SelStart < ViewEnd)
        {
            float x0 = Mathf.Max(0f, XOf(SelStart)), x1 = Mathf.Min(r.width, XOf(SelEnd));
            p.fillColor = SelFill;
            p.BeginPath();
            p.MoveTo(new Vector2(x0, 0f));
            p.LineTo(new Vector2(x1, 0f));
            p.LineTo(new Vector2(x1, r.height));
            p.LineTo(new Vector2(x0, r.height));
            p.ClosePath();
            p.Fill();
        }

        float laneH = r.height / channels;

        // 中线与声道分隔线
        p.strokeColor = CenterColor;
        p.lineWidth = 1f;
        p.BeginPath();
        for (int c = 0; c < channels; c++)
        {
            float cy = laneH * c + laneH * 0.5f;
            p.MoveTo(new Vector2(0f, cy));
            p.LineTo(new Vector2(r.width, cy));
            if (c > 0) { p.MoveTo(new Vector2(0f, laneH * c)); p.LineTo(new Vector2(r.width, laneH * c)); }
        }
        p.Stroke();

        p.strokeColor = WaveColor;
        p.lineWidth = 1f;
        if (span <= (long)w * 3)
        {
            // 放大到足够近：逐采样折线
            for (int c = 0; c < channels; c++)
            {
                float cy = laneH * c + laneH * 0.5f, half = laneH * 0.48f;
                p.BeginPath();
                bool first = true;
                long last = Math.Min(ViewEnd, Frames - 1);
                for (long f = ViewStart; f <= last; f++)
                {
                    var pt = new Vector2(XOf(f), cy - samples[f * channels + c] * half);
                    if (first) { p.MoveTo(pt); first = false; }
                    else p.LineTo(pt);
                }
                p.Stroke();
            }
        }
        else
        {
            // 缩小视图：每像素列画 min/max 峰值
            p.BeginPath();
            for (int c = 0; c < channels; c++)
            {
                float cy = laneH * c + laneH * 0.5f, half = laneH * 0.48f;
                for (int x = 0; x < w; x++)
                {
                    long f0 = ViewStart + span * x / w;
                    long f1 = Math.Min(Math.Max(f0 + 1, ViewStart + span * (x + 1) / w), Frames);
                    long stride = Math.Max(1, (f1 - f0) / 256);
                    float mn = 1f, mx = -1f;
                    for (long f = f0; f < f1; f += stride)
                    {
                        float v = samples[f * channels + c];
                        if (v < mn) mn = v;
                        if (v > mx) mx = v;
                    }
                    if (mx < mn) continue;
                    p.MoveTo(new Vector2(x + 0.5f, cy - mx * half));
                    p.LineTo(new Vector2(x + 0.5f, cy - mn * half + 1f));
                }
            }
            p.Stroke();
        }

        // 选区边界线、边缘把手与播放光标
        if (HasSelection)
        {
            DrawVLine(p, XOf(SelStart), r, SelEdge);
            DrawVLine(p, XOf(SelEnd), r, SelEdge);
            DrawHandle(p, XOf(SelStart), r);
            DrawHandle(p, XOf(SelEnd), r);
        }
        if (Playhead >= ViewStart && Playhead <= ViewEnd)
            DrawVLine(p, XOf(Playhead), r, PlayheadColor);
    }

    // 选区边缘的上下三角把手，提示此处可拖拽调整
    static void DrawHandle(Painter2D p, float x, Rect r)
    {
        if (x < -1f || x > r.width + 1f) return;
        p.fillColor = SelEdge;
        p.BeginPath();
        p.MoveTo(new Vector2(x - 5f, 0f));
        p.LineTo(new Vector2(x + 5f, 0f));
        p.LineTo(new Vector2(x, 8f));
        p.ClosePath();
        p.Fill();
        p.BeginPath();
        p.MoveTo(new Vector2(x - 5f, r.height));
        p.LineTo(new Vector2(x + 5f, r.height));
        p.LineTo(new Vector2(x, r.height - 8f));
        p.ClosePath();
        p.Fill();
    }

    static void DrawVLine(Painter2D p, float x, Rect r, Color c)
    {
        if (x < -1f || x > r.width + 1f) return;
        p.strokeColor = c;
        p.lineWidth = 1f;
        p.BeginPath();
        p.MoveTo(new Vector2(x, 0f));
        p.LineTo(new Vector2(x, r.height));
        p.Stroke();
    }
}
#endif
