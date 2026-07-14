#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 裁切模块（AU 风格）：在波形上拖出选区后，「裁切为选区」只保留选中部分，「删除选区」剪掉选中部分前后拼接；
/// 支持多步撤销，结果可「另存为 WAV」或覆盖 .wav 源文件（16-bit PCM）。
/// </summary>
public class AudioTrimModule : AudioEditorModule
{
    public override string Title => "裁切";
    public override int Order => 0;

    Button cropBtn, delBtn, undoBtn, saveAsBtn, overwriteBtn;
    Label status;

    protected override VisualElement Build()
    {
        var root = new VisualElement { style = { paddingTop = 6f, paddingBottom = 4f } };

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        row.Add(cropBtn = new Button(Crop) { text = "裁切为选区", tooltip = "只保留选中部分（AU 的 Trim）", style = { height = 26f } });
        row.Add(delBtn = new Button(Delete) { text = "删除选区", tooltip = "剪掉选中部分，前后拼接", style = { height = 26f } });
        row.Add(undoBtn = new Button(Undo) { text = "撤销", style = { height = 26f } });
        row.Add(new VisualElement { style = { flexGrow = 1f } });
        row.Add(saveAsBtn = new Button(SaveAs) { text = "另存为 WAV", style = { height = 26f } });
        row.Add(overwriteBtn = new Button(Overwrite)
        { text = "覆盖源文件", tooltip = "仅源文件为 .wav 时可用；其他格式请用「另存为 WAV」", style = { height = 26f } });
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
        bool sel = has && Ctx.Waveform.HasSelection;
        cropBtn.SetEnabled(sel);
        delBtn.SetEnabled(sel);
        undoBtn.SetEnabled(Ctx.CanUndo);
        saveAsBtn.SetEnabled(has);
        string src = SourcePath();
        overwriteBtn.SetEnabled(has && src != null && src.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
    }

    string SourcePath() => Ctx.SourceClip ? AssetDatabase.GetAssetPath(Ctx.SourceClip) : null;

    // ---- 编辑 ----

    void Crop()
    {
        var wf = Ctx.Waveform;
        if (!wf.HasSelection) return;
        long s = wf.SelStart, e = wf.SelEnd;
        int ch = Ctx.Channels;
        var dst = new float[(e - s) * ch];
        Array.Copy(Ctx.Samples, s * ch, dst, 0, dst.Length);
        Ctx.PushUndo();
        Ctx.SetSamples(dst, true);
        status.text = "已裁切为选区（未保存）。";
    }

    void Delete()
    {
        var wf = Ctx.Waveform;
        if (!wf.HasSelection) return;
        long s = wf.SelStart, e = wf.SelEnd;
        int ch = Ctx.Channels;
        var src = Ctx.Samples;
        var dst = new float[src.Length - (e - s) * ch];
        Array.Copy(src, 0, dst, 0, s * ch);
        Array.Copy(src, e * ch, dst, s * ch, src.Length - e * ch);
        Ctx.PushUndo();
        Ctx.SetSamples(dst, true);
        status.text = "已删除选区（未保存）。";
    }

    void Undo()
    {
        Ctx.Undo();
        status.text = "已撤销。";
    }

    // ---- 保存 ----

    void SaveAs()
    {
        if (Ctx.Frames == 0) return;
        string src = SourcePath();
        string dir = string.IsNullOrEmpty(src) ? "Assets" : Path.GetDirectoryName(src).Replace('\\', '/');
        string name = (Ctx.SourceClip ? Ctx.SourceClip.name : "audio") + "_Trim";
        string path = EditorUtility.SaveFilePanelInProject("另存为 WAV", name, "wav", "选择保存位置", dir);
        if (string.IsNullOrEmpty(path)) return;
        WriteWav(path);
        status.text = "已保存: " + path;
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
    }

    void Overwrite()
    {
        string src = SourcePath();
        if (Ctx.Frames == 0 || src == null || !src.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return;
        if (!EditorUtility.DisplayDialog("覆盖源文件", $"将用当前波形覆盖：\n{src}\n（16-bit PCM，文件层面不可恢复）。继续？", "覆盖", "取消"))
            return;
        WriteWav(src);
        status.text = "已覆盖源文件: " + src;
    }

    void WriteWav(string assetPath)
    {
        File.WriteAllBytes(assetPath, AudioEditUtil.EncodeWav16(Ctx.Samples, Ctx.Channels, Ctx.Frequency));
        AssetDatabase.ImportAsset(assetPath);
        Ctx.MarkSaved();
    }
}
#endif
