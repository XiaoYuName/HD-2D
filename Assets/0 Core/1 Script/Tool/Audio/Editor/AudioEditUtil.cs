#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 音频编辑器底层工具：读取 AudioClip 采样（非 DecompressOnLoad 时临时改导入设置读取后还原）、
/// 编码 16-bit PCM WAV、通过反射调用编辑器内置预览播放（UnityEditor.AudioUtil）。
/// </summary>
public static class AudioEditUtil
{
    static Type audioUtil;
    static Type AudioUtil => audioUtil ??= typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

    static MethodInfo playM, stopM, posM, isPlayingM;
    static AudioClip lastPreview;

    static MethodInfo Find(params string[] names)
    {
        foreach (var n in names)
            foreach (var m in AudioUtil.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (m.Name == n)
                    return m;
        return null;
    }

    // ---- 预览播放（AudioUtil 反射，兼容新旧方法名） ----

    public static void PlayPreview(AudioClip clip, bool loop)
    {
        playM ??= Find("PlayPreviewClip", "PlayClip");
        if (playM == null) { Debug.LogWarning("[AudioEditor] 未找到 AudioUtil.PlayPreviewClip，无法预览播放。"); return; }
        lastPreview = clip;
        var ps = playM.GetParameters();
        var args = new object[ps.Length];
        args[0] = clip;
        for (int i = 1; i < ps.Length; i++)
            args[i] = ps[i].ParameterType == typeof(bool) ? loop : (object)0;
        playM.Invoke(null, args);
    }

    public static void StopPreview()
    {
        stopM ??= Find("StopAllPreviewClips", "StopAllClips");
        stopM?.Invoke(null, null);
    }

    public static bool IsPreviewPlaying()
    {
        isPlayingM ??= Find("IsPreviewClipPlaying", "IsClipPlaying");
        if (isPlayingM == null) return false;
        object r = isPlayingM.Invoke(null, isPlayingM.GetParameters().Length == 0 ? null : new object[] { lastPreview });
        return r is bool b && b;
    }

    /// <summary>当前预览播放位置（秒）。</summary>
    public static float GetPreviewPosition()
    {
        posM ??= Find("GetPreviewClipPosition", "GetClipPosition");
        if (posM == null) return 0f;
        object r = posM.Invoke(null, posM.GetParameters().Length == 0 ? null : new object[] { lastPreview });
        return Convert.ToSingle(r);
    }

    // ---- 采样读取 ----

    public static bool TryGetSamples(AudioClip clip, out float[] samples)
    {
        samples = null;
        if (clip == null || clip.samples <= 0) return false;

        var buf = new float[clip.samples * clip.channels];
        if (clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.GetData(buf, 0))
        { samples = buf; return true; }

        string path = AssetDatabase.GetAssetPath(clip);
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp == null)
        {
            if (clip.GetData(buf, 0)) samples = buf;
            return samples != null;
        }

        // 非 DecompressOnLoad 无法直接 GetData：临时改导入设置读取后还原
        var saved = imp.defaultSampleSettings;
        var tmp = saved;
        tmp.loadType = AudioClipLoadType.DecompressOnLoad;
        try
        {
            imp.defaultSampleSettings = tmp;
            imp.SaveAndReimport();
            var re = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var buf2 = new float[re.samples * re.channels];
            if (re.GetData(buf2, 0)) samples = buf2;
        }
        finally
        {
            imp.defaultSampleSettings = saved;
            imp.SaveAndReimport();
        }
        return samples != null;
    }

    // ---- WAV 编码 ----

    /// <summary>交错 float 采样 → 16-bit PCM WAV 字节。</summary>
    public static byte[] EncodeWav16(float[] samples, int channels, int frequency)
    {
        int dataLen = samples.Length * 2;
        using var ms = new MemoryStream(44 + dataLen);
        using var w = new BinaryWriter(ms);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataLen);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);                       // PCM
        w.Write((short)channels);
        w.Write(frequency);
        w.Write(frequency * channels * 2);       // byte rate
        w.Write((short)(channels * 2));          // block align
        w.Write((short)16);                      // bits
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataLen);
        foreach (float f in samples)
            w.Write((short)Mathf.Clamp(Mathf.RoundToInt(f * 32767f), short.MinValue, short.MaxValue));
        return ms.ToArray();
    }
}
#endif
