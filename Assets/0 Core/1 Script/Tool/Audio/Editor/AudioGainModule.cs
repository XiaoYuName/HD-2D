#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 音量模块：按分贝放大/缩小选区或整段音频（正值增益、负值衰减），另提供「归一化到 -0.3dB」。
/// 应用时限幅到 [-1,1]（保存为 16-bit PCM 会截断），发生限幅会在状态栏提示数量。支持撤销。
/// </summary>
public class AudioGainModule : AudioEditorModule
{
    public override string Title => "音量";
    public override int Order => 1;

    Slider dbSlider;
    Button applySelBtn, applyAllBtn, normalizeBtn;
    Label status;

    protected override VisualElement Build()
    {
        var root = new VisualElement { style = { paddingTop = 6f, paddingBottom = 4f } };

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        dbSlider = new Slider("增益 (dB)", -24f, 24f) { value = 0f, showInputField = true, style = { flexGrow = 1f } };
        dbSlider.RegisterValueChangedCallback(_ => Refresh());
        row.Add(dbSlider);
        row.Add(applySelBtn = new Button(() => Apply(true)) { text = "应用到选区", style = { height = 26f } });
        row.Add(applyAllBtn = new Button(() => Apply(false)) { text = "应用到全部", style = { height = 26f } });
        row.Add(normalizeBtn = new Button(Normalize)
        { text = "归一化", tooltip = "把整段音频的峰值提升/压低到 -0.3dB", style = { height = 26f } });
        root.Add(row);

        status = new Label { style = { marginTop = 4f, whiteSpace = WhiteSpace.Normal } };
        root.Add(status);

        Ctx.DataChanged += Refresh;
        Ctx.Waveform.SelectionChanged += Refresh;
        Refresh();
        return root;
    }

    void Refresh()
    {
        bool has = Ctx.Frames > 0;
        applySelBtn.SetEnabled(has && Ctx.Waveform.HasSelection && !Mathf.Approximately(dbSlider.value, 0f));
        applyAllBtn.SetEnabled(has && !Mathf.Approximately(dbSlider.value, 0f));
        normalizeBtn.SetEnabled(has);
    }

    void Apply(bool selOnly)
    {
        if (Ctx.Frames == 0) return;
        var wf = Ctx.Waveform;
        long s = 0, e = Ctx.Frames;
        if (selOnly)
        {
            if (!wf.HasSelection) return;
            s = wf.SelStart;
            e = wf.SelEnd;
        }
        float db = dbSlider.value;
        int clipped = ApplyGain(s, e, Mathf.Pow(10f, db / 20f));
        status.text = $"已对{(selOnly ? "选区" : "全部")}应用 {db:+0.0;-0.0} dB" +
                      (clipped > 0 ? $"，有 {clipped} 个采样被限幅到 ±1（建议降低增益）" : "") + "（未保存）。";
    }

    void Normalize()
    {
        if (Ctx.Frames == 0) return;
        float peak = 0f;
        foreach (float v in Ctx.Samples)
            peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak < 1e-6f) { status.text = "音频近似静音，无法归一化。"; return; }
        float target = Mathf.Pow(10f, -0.3f / 20f);
        ApplyGain(0, Ctx.Frames, target / peak);
        status.text = $"已归一化：峰值 {20f * Mathf.Log10(peak):0.0} dB → -0.3 dB（未保存）。";
    }

    /// <summary>对 [s,e) 帧区间乘增益并限幅，返回被限幅的采样数。</summary>
    int ApplyGain(long s, long e, float factor)
    {
        Ctx.PushUndo();
        var data = Ctx.Samples;
        int ch = Ctx.Channels, clipped = 0;
        for (long i = s * ch; i < e * ch; i++)
        {
            float v = data[i] * factor;
            if (v > 1f) { v = 1f; clipped++; }
            else if (v < -1f) { v = -1f; clipped++; }
            data[i] = v;
        }
        Ctx.SetSamples(data, true);
        return clipped;
    }
}
#endif
