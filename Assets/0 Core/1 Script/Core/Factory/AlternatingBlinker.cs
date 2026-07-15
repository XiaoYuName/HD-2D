using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一组 Image 交替闪烁：按下标奇偶分两相，每隔 <see cref="interval"/> 在「亮色 / 暗色」间互换，
/// 形成相邻灯交替明灭的跑马灯效果（如传送带流水灯）。挂到任意物体，拖入若干 Image（如 5 个）即可。
/// </summary>
public class AlternatingBlinker : MonoBehaviour
{
    [LabelText("交替闪烁的图片(按摆放顺序拖入)")][SerializeField] List<Image> images = new ();
    [LabelText("亮色")][SerializeField] Color onColor = new (0.85f, 0.7f, 0.4f, 1f);
    [LabelText("暗色")][SerializeField] Color offColor = new (0.72f, 0.72f, 0.7f, 1f);
    [LabelText("切换间隔(秒)"), MinValue(0.01f)][SerializeField] float interval = 0.4f;
    [LabelText("平滑过渡(关闭则硬切)")][SerializeField] bool smooth = true;
    [LabelText("启用时自动播放")][SerializeField] bool playOnEnable = true;

    Coroutine ct;
    bool phase;   // false / true 两相，每个 interval 翻转一次

    void OnEnable()
    {
        if(playOnEnable)
            Play();
    }

    void OnDisable() => Stop();

    [Button("播放")]
    public void Play()
    {
        Stop();
        phase = false;
        ct = StartCoroutine(Run());
    }

    [Button("停止")]
    public void Stop()
    {
        if(ct != null)
        {
            StopCoroutine(ct);
            ct = null;
        }
    }

    IEnumerator Run()
    {
        while(true)
        {
            if(smooth)
            {
                for(float t = 0f; t < interval; t += Time.deltaTime)
                {
                    Apply(t / interval);
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(interval);
            }
            Apply(1f);
            phase = !phase;
        }
    }

    // k：0→1 当前相的过渡进度。偶数下标用 phase、奇数下标取反，二者互为明暗；
    // 相翻转前每盏灯停在上一相的目标色，故从「目标色的反色」插值到「目标色」即为正确过渡。
    void Apply(float k)
    {
        for(int i = 0; i < images.Count; i++)
        {
            bool on = (i % 2 == 0) ? phase : !phase;
            Color to = on ? onColor : offColor;
            images[i].color = smooth ? Color.Lerp(on ? offColor : onColor, to, k) : to;
        }
    }
}
