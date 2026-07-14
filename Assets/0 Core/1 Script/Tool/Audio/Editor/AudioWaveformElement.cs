#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 波形显示与选区交互控件（AU 风格）：左键拖拽创建选区、拖动选区边缘微调、单击定位播放光标；
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

    enum DragMode { None, NewSel, EdgeStart, EdgeEnd }
    DragMode drag;
    long dragAnchor;
    Vector2 downPos;
    bool dragMoved;

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
        RegisterCallback<WheelEvent>(OnWheel);
    }

    public void SetData(float[] s, int ch, int freq)
    {
        samples = s;
        channels = Mathf.Max(1, ch);
        Frequency = freq;
        Frames = s == null ? 0 : s.Length / channels;
        ViewStart = 0;
        ViewEnd = Math.Max(1, Frames);
        SelStart = SelEnd = -1;
        Playhead = 0;
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

    void OnDown(PointerDownEvent e)
    {
        if (e.button != 0 || Frames == 0) return;
        Focus();
        downPos = e.localPosition;
        dragMoved = false;
        float x = e.localPosition.x;
        if (HasSelection && Mathf.Abs(x - XOf(SelStart)) <= 6f) { drag = DragMode.EdgeStart; dragAnchor = SelEnd; }
        else if (HasSelection && Mathf.Abs(x - XOf(SelEnd)) <= 6f) { drag = DragMode.EdgeEnd; dragAnchor = SelStart; }
        else { drag = DragMode.NewSel; dragAnchor = FrameAt(x); }
        this.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    void OnMove(PointerMoveEvent e)
    {
        if (drag == DragMode.None || !this.HasPointerCapture(e.pointerId)) return;
        if (((Vector2)e.localPosition - downPos).sqrMagnitude > 9f) dragMoved = true;
        if (!dragMoved) return;
        long f = FrameAt(e.localPosition.x);
        SetSelection(Math.Min(dragAnchor, f), Math.Max(dragAnchor, f));
    }

    void OnUp(PointerUpEvent e)
    {
        if (drag == DragMode.None) return;
        this.ReleasePointer(e.pointerId);
        if (!dragMoved && drag == DragMode.NewSel)
        {
            ClearSelection();
            SetPlayhead(FrameAt(e.localPosition.x));
        }
        drag = DragMode.None;
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

        // 选区边界线与播放光标
        if (HasSelection)
        {
            DrawVLine(p, XOf(SelStart), r, SelEdge);
            DrawVLine(p, XOf(SelEnd), r, SelEdge);
        }
        if (Playhead >= ViewStart && Playhead <= ViewEnd)
            DrawVLine(p, XOf(Playhead), r, PlayheadColor);
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
