using System.Collections.Generic;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UGUI 闪光池：笔刷拖尾按距离撒星，画作区域按间隔自动冒星；特殊稿件发循环彩光且更强烈。</summary>
public sealed class MachiRoomSparkleEffect : MonoBehaviour
{
    [LabelText("闪光图片"), SerializeField] Sprite[] sparkleSprites;
    [LabelText("自动闪光区域"), Tooltip("留空则只做手动拖尾闪光"), SerializeField] RectTransform ambientArea;
    [LabelText("自动闪光间隔"), MinValue(0.01f), SerializeField] float ambientInterval = 0.12f;
    [LabelText("特殊稿件强度倍率"), MinValue(1f), SerializeField] float specialIntensityScale = 2f;

    readonly List<Sparkle> sparkles = new();
    MachiRoomGameConfig config;
    Vector2 lastTrailPos;
    float ambientTimer;
    float sparkleHue;
    bool isSpecial;
    bool hasLastTrailPos;
    bool isAmbientPlaying;

    /// <summary>特殊稿件下按时间循环的彩光色，普通稿件返回配置的固定色。</summary>
    public Color CurrentColor => isSpecial
        ? Color.HSVToRGB(
            Mathf.Repeat(Time.unscaledTime / config.SparkleSpecialCycleDura, 1f),
            config.SparkleSpecialSaturation,
            1f)
        : config.SparkleNormalColor;

    public void Play(MachiRoomGameConfig gameConfig, bool special)
    {
        config = gameConfig;
        isSpecial = special;
        hasLastTrailPos = false;
        ambientTimer = 0f;
        sparkleHue = 0f;
        isAmbientPlaying = ambientArea != null;
    }

    public void Stop()
    {
        isAmbientPlaying = false;
        hasLastTrailPos = false;
        for (int i = 0; i < sparkles.Count; i++)
        {
            sparkles[i].Seq.Stop();
            sparkles[i].Rtf.gameObject.SetActive(false);
        }
    }

    /// <summary>拖尾用：屏幕点距上一枚闪光超过 minDistance 才撒新的一枚。</summary>
    public void TryEmitTrail(Vector2 screenPos, Camera eventCamera, float minDistance)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Root,
                screenPos,
                eventCamera,
                out Vector2 localPoint))
            return;
        if (hasLastTrailPos && Vector2.Distance(localPoint, lastTrailPos) < minDistance)
            return;

        lastTrailPos = localPoint;
        hasLastTrailPos = true;
        Emit(localPoint, 1f);
    }

    public void ResetTrail()
    {
        hasLastTrailPos = false;
    }

    public void Emit(Vector2 anchoredPos, float sizeScale = 1f)
    {
        Sparkle sparkle = GetFreeSparkle();
        if (sparkle == null)
            return;

        float life = config.SparkleLifetime;
        float size = UnityEngine.Random.Range(config.SparkleSizeRange.x, config.SparkleSizeRange.y)
            * sizeScale;
        Vector2 from = anchoredPos + UnityEngine.Random.insideUnitCircle * config.SparkleSpreadRadius;
        Vector2 to = from + Vector2.up * config.SparkleRiseDistance;
        Quaternion fromRot = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        Quaternion toRot = fromRot * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-120f, 120f));

        sparkle.Seq.Stop();
        sparkle.Image.sprite = sparkleSprites[UnityEngine.Random.Range(0, sparkleSprites.Length)];
        sparkle.Image.color = NextColor();
        sparkle.Rtf.sizeDelta = new Vector2(size, size);
        sparkle.Rtf.anchoredPosition = from;
        sparkle.Rtf.localRotation = fromRot;
        sparkle.Rtf.localScale = Vector3.zero;
        sparkle.Rtf.gameObject.SetActive(true);

        sparkle.Seq = Sequence.Create()
            .Group(Tween.UIAnchoredPosition(sparkle.Rtf, from, to, life, Ease.OutCubic))
            .Group(Tween.LocalRotation(sparkle.Rtf, fromRot, toRot, life, Ease.OutCubic))
            .Group(Tween.Scale(sparkle.Rtf, Vector3.zero, Vector3.one, life * 0.3f, Ease.OutBack))
            .Group(Tween.Scale(
                sparkle.Rtf,
                Vector3.one,
                Vector3.zero,
                life * 0.7f,
                Ease.InQuad,
                startDelay: life * 0.3f))
            .Group(Tween.Alpha(
                sparkle.Image,
                1f,
                0f,
                life * 0.6f,
                Ease.InQuad,
                startDelay: life * 0.4f))
            .ChainCallback(sparkle, s => s.Rtf.gameObject.SetActive(false));
    }

    void Update()
    {
        if (!isAmbientPlaying)
            return;

        float intensity = isSpecial ? specialIntensityScale : 1f;
        ambientTimer -= Time.unscaledDeltaTime;
        if (ambientTimer > 0f)
            return;

        ambientTimer = ambientInterval / intensity;
        Emit(RandomPointInAmbientArea(), isSpecial ? Mathf.Sqrt(specialIntensityScale) : 1f);
    }

    void OnDisable()
    {
        Stop();
    }

    /// <summary>自动闪光区域内随机取点，换算到闪光父节点坐标系。</summary>
    Vector2 RandomPointInAmbientArea()
    {
        Rect rect = ambientArea.rect;
        Vector3 worldPoint = ambientArea.TransformPoint(new Vector3(
            UnityEngine.Random.Range(rect.xMin, rect.xMax),
            UnityEngine.Random.Range(rect.yMin, rect.yMax),
            0f));
        return Root.InverseTransformPoint(worldPoint);
    }

    /// <summary>特殊稿件逐颗递进色相发彩光，普通稿件用固定颜色。</summary>
    Color NextColor()
    {
        if (!isSpecial)
            return config.SparkleNormalColor;

        sparkleHue = Mathf.Repeat(sparkleHue + config.SparkleSpecialHueStep, 1f);
        return Color.HSVToRGB(sparkleHue, config.SparkleSpecialSaturation, 1f);
    }

    Sparkle GetFreeSparkle()
    {
        for (int i = 0; i < sparkles.Count; i++)
            if (!sparkles[i].Rtf.gameObject.activeSelf)
                return sparkles[i];

        int maxCount = Mathf.RoundToInt(
            config.SparkleMaxCount * (isSpecial ? specialIntensityScale : 1f));
        if (sparkles.Count >= maxCount)
            return null;

        GameObject go = new(
            "Sparkle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform rtf = (RectTransform)go.transform;
        rtf.SetParent(Root, false);
        rtf.anchorMin = rtf.anchorMax = rtf.pivot = new Vector2(0.5f, 0.5f);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        go.SetActive(false);

        Sparkle sparkle = new() { Rtf = rtf, Image = image };
        sparkles.Add(sparkle);
        return sparkle;
    }

    RectTransform Root => (RectTransform)transform;

    sealed class Sparkle
    {
        public RectTransform Rtf;
        public Image Image;
        public Sequence Seq;
    }
}
