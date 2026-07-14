#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 音频编辑器共享上下文：当前剪辑、采样数据（交错 float）、脏标记与撤销栈。各功能模块通过它读写数据。
/// </summary>
public class AudioEditorContext
{
    public AudioClip SourceClip;
    public float[] Samples;
    public int Channels = 1;
    public int Frequency = 44100;
    public AudioWaveformElement Waveform;

    public bool Dirty { get; private set; }
    public int Frames => Samples == null ? 0 : Samples.Length / Channels;
    public event Action DataChanged;

    readonly List<float[]> undoStack = new();
    public bool CanUndo => undoStack.Count > 0;

    public void SetSamples(float[] samples, bool dirty)
    {
        Samples = samples;
        Dirty = dirty;
        Waveform?.SetData(samples, Channels, Frequency);
        DataChanged?.Invoke();
    }

    public void PushUndo()
    {
        undoStack.Add((float[])Samples.Clone());
        if (undoStack.Count > 16) undoStack.RemoveAt(0);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var s = undoStack[^1];
        undoStack.RemoveAt(undoStack.Count - 1);
        SetSamples(s, true);
    }

    public void ResetUndo() => undoStack.Clear();

    public void MarkSaved()
    {
        Dirty = false;
        DataChanged?.Invoke();
    }
}

/// <summary>
/// 音频编辑器功能模块基类：继承并实现 <see cref="Build"/> 即自动出现为窗口底部的新页签（TypeCache 扫描，无需注册）。
/// </summary>
public abstract class AudioEditorModule
{
    protected AudioEditorContext Ctx { get; private set; }
    public abstract string Title { get; }
    public virtual int Order => 0;

    public VisualElement CreateUI(AudioEditorContext ctx)
    {
        Ctx = ctx;
        return Build();
    }

    protected abstract VisualElement Build();
}

/// <summary>
/// 音频编辑器主窗口（UIToolkit）：顶部选择 AudioClip 与播放控制，中部波形（拖拽选区 / 滚轮缩放 / Shift+滚轮平移 /
/// 单击定位光标 / 空格播放），底部为功能模块页签（裁切等，可扩展）。
/// </summary>
public class AudioEditorWindow : EditorWindow
{
    AudioEditorContext ctx;
    ObjectField clipField;
    Label infoLabel;
    AudioWaveformElement waveform;
    Scroller scroller;
    Toggle loopToggle;

    AudioClip previewClip;
    long playFromFrame;
    bool playing;
    bool syncingScroll;
    IVisualElementScheduledItem playPoll;

    [MenuItem("Tools/Audio/音频编辑器")]
    static void Open() => GetWindow<AudioEditorWindow>("音频编辑器").minSize = new Vector2(720f, 420f);

    [MenuItem("Assets/用音频编辑器打开", true)]
    static bool OpenAssetValidate() => Selection.activeObject is AudioClip;

    [MenuItem("Assets/用音频编辑器打开")]
    static void OpenAsset()
    {
        pendingClip = Selection.activeObject as AudioClip;
        var win = GetWindow<AudioEditorWindow>("音频编辑器");
        win.minSize = new Vector2(720f, 420f);
        if (win.clipField != null && pendingClip != null) { win.clipField.value = pendingClip; pendingClip = null; }
    }

    static AudioClip pendingClip;

    void CreateGUI()
    {
        ctx = new AudioEditorContext();
        var root = rootVisualElement;
        root.style.paddingTop = root.style.paddingBottom = root.style.paddingLeft = root.style.paddingRight = 6f;

        // 顶部：剪辑选择 + 播放控制
        var bar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexShrink = 0f } };
        clipField = new ObjectField { objectType = typeof(AudioClip), allowSceneObjects = false, style = { flexGrow = 1f } };
        clipField.RegisterValueChangedCallback(OnClipChanged);
        bar.Add(clipField);
        bar.Add(new Button(() => Play(false)) { text = "▶ 播放", tooltip = "从光标处播放全部（空格）" });
        bar.Add(new Button(() => Play(true)) { text = "▶ 选区", tooltip = "只播放选中区域" });
        bar.Add(new Button(StopPlayback) { text = "■ 停止" });
        loopToggle = new Toggle("循环") { style = { marginLeft = 4f } };
        bar.Add(loopToggle);
        root.Add(bar);

        infoLabel = new Label { style = { marginTop = 2f, marginBottom = 2f, flexShrink = 0f } };
        root.Add(infoLabel);

        // 中部：波形 + 横向滚动条
        waveform = new AudioWaveformElement();
        ctx.Waveform = waveform;
        root.Add(waveform);
        scroller = new Scroller(0f, 1f, OnScroll, SliderDirection.Horizontal) { style = { flexShrink = 0f } };
        root.Add(scroller);

        waveform.SelectionChanged += RefreshInfo;
        waveform.ViewChanged += SyncScroller;
        ctx.DataChanged += () => { RefreshInfo(); SyncScroller(); };
        waveform.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode != KeyCode.Space) return;
            if (playing) StopPlayback();
            else Play(waveform.HasSelection);
            e.StopPropagation();
        });

        // 底部：功能模块页签（TypeCache 自动发现）
        var tabs = new TabView { style = { flexShrink = 0f, marginTop = 4f } };
        foreach (var m in TypeCache.GetTypesDerivedFrom<AudioEditorModule>()
                     .Where(t => !t.IsAbstract)
                     .Select(t => (AudioEditorModule)Activator.CreateInstance(t))
                     .OrderBy(m => m.Order))
        {
            var tab = new Tab(m.Title);
            tab.Add(m.CreateUI(ctx));
            tabs.Add(tab);
        }
        root.Add(tabs);

        RefreshInfo();
        SyncScroller();
        if (pendingClip != null) { clipField.value = pendingClip; pendingClip = null; }
    }

    void OnDisable() => StopPlayback();

    // ---- 剪辑加载 ----

    void OnClipChanged(ChangeEvent<UnityEngine.Object> e)
    {
        var clip = e.newValue as AudioClip;
        if (clip == ctx.SourceClip) return;
        if (ctx.Dirty && !EditorUtility.DisplayDialog("音频编辑器", "当前修改尚未保存，切换后将丢失。继续？", "继续", "取消"))
        {
            clipField.SetValueWithoutNotify(e.previousValue);
            return;
        }
        LoadClip(clip);
    }

    void LoadClip(AudioClip clip)
    {
        StopPlayback();
        ctx.SourceClip = clip;
        ctx.ResetUndo();
        if (clip == null)
        {
            ctx.SetSamples(null, false);
            return;
        }

        int ch = clip.channels, freq = clip.frequency;
        if (!AudioEditUtil.TryGetSamples(clip, out var samples))
        {
            EditorUtility.DisplayDialog("音频编辑器", $"无法读取采样数据：{clip.name}", "确定");
            clipField.SetValueWithoutNotify(null);
            ctx.SourceClip = null;
            ctx.SetSamples(null, false);
            return;
        }
        ctx.Channels = ch;
        ctx.Frequency = freq;
        ctx.SetSamples(samples, false);
    }

    // ---- 播放（用当前内存数据建临时剪辑，编辑结果可即时试听） ----

    void Play(bool selectionOnly)
    {
        if (ctx.Frames == 0) return;
        StopPlayback();

        long s = 0, e = ctx.Frames;
        if (selectionOnly && waveform.HasSelection) { s = waveform.SelStart; e = waveform.SelEnd; }
        else if (waveform.Playhead > 0 && waveform.Playhead < ctx.Frames - 1) s = waveform.Playhead;
        int frames = (int)(e - s);
        if (frames <= 0) return;

        var seg = new float[frames * ctx.Channels];
        Array.Copy(ctx.Samples, s * ctx.Channels, seg, 0, seg.Length);
        previewClip = AudioClip.Create("AudioEditorPreview", frames, ctx.Channels, ctx.Frequency, false);
        previewClip.SetData(seg, 0);
        AudioEditUtil.PlayPreview(previewClip, loopToggle.value);

        playing = true;
        playFromFrame = s;
        playPoll?.Pause();
        playPoll = waveform.schedule.Execute(PollPlayhead).Every(30);
    }

    void PollPlayhead()
    {
        if (!AudioEditUtil.IsPreviewPlaying()) { StopPlayback(); return; }
        waveform.SetPlayhead(playFromFrame + (long)(AudioEditUtil.GetPreviewPosition() * ctx.Frequency));
    }

    void StopPlayback()
    {
        AudioEditUtil.StopPreview();
        playing = false;
        playPoll?.Pause();
        if (previewClip != null) { DestroyImmediate(previewClip); previewClip = null; }
    }

    // ---- 显示同步 ----

    void OnScroll(float v)
    {
        if (syncingScroll) return;
        long span = waveform.ViewEnd - waveform.ViewStart;
        waveform.SetView((long)v, (long)v + span);
    }

    void SyncScroller()
    {
        if (scroller == null) return;
        syncingScroll = true;
        long frames = waveform.Frames, span = waveform.ViewEnd - waveform.ViewStart;
        scroller.lowValue = 0f;
        scroller.highValue = Mathf.Max(0f, frames - span);
        scroller.value = waveform.ViewStart;
        scroller.Adjust(frames > 0 ? span / (float)frames : 1f);
        scroller.SetEnabled(frames > span);
        syncingScroll = false;
    }

    void RefreshInfo()
    {
        if (ctx.Frames == 0)
        {
            infoLabel.text = "拖入或选择一个 AudioClip 开始编辑。拖拽选区，单击定位光标，滚轮缩放，Shift+滚轮平移，空格播放。";
        }
        else
        {
            string sel = waveform.HasSelection
                ? $"  |  选区 {T(waveform.SelStart)} ~ {T(waveform.SelEnd)}（{T(waveform.SelEnd - waveform.SelStart)}）"
                : $"  |  光标 {T(waveform.Playhead)}";
            infoLabel.text = $"{T(ctx.Frames)}  |  {ctx.Frequency} Hz  |  {ctx.Channels} 声道{sel}{(ctx.Dirty ? "  |  ● 未保存" : "")}";
        }
        titleContent.text = ctx.Dirty ? "音频编辑器 *" : "音频编辑器";
    }

    string T(long frame) => TimeSpan.FromSeconds(frame / (double)ctx.Frequency).ToString(@"m\:ss\.fff");
}
#endif
