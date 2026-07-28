using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;

public sealed class MachiRoomScratchTicket : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerMoveHandler,
    IDragHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    const string ScratchMaskProperty = "_ScratchMask";
    const string SpriteUvRectProperty = "_SpriteUvRect";

    [LabelText("刮除区域"), SerializeField] RectTransform scratchAreaRtf;
    [LabelText("跟随画笔"), SerializeField] RectTransform penRtf;
    [LabelText("遮挡图"), SerializeField] Image maskImage;
    [LabelText("刮除 Shader"), SerializeField] Shader scratchShader;
    [LabelText("笔刷闪光"), SerializeField] MachiRoomSparkleEffect sparkleEffect;
    [LabelText("笔尖星星"), Tooltip("特殊作品时跟着彩光变色"), SerializeField] Graphic[] penStars;

    Material scratchMaterial;
    Texture2D scratchMaskTex;
    Color32[] scratchPixels;
    Action onScratchCompleted;
    MachiRoomGameConfig config;
    Vector2 lastScratchPixel;
    Vector2 penOffset;
    float brushRadius;
    float completionRatio;
    int textureSize;
    int scratchedPixelCount;
    bool hasLastScratchPixel;
    bool isPointerDown;
    bool isPointerInside;
    bool isScratchActive;
    bool isSpecialDraft;

    /// <param name="isSpecial">特殊作品：闪光改发彩光，笔尖星星一起循环变色</param>
    public void StartScratch(MachiRoomGameConfig gameConfig, bool isSpecial, Action completed)
    {
        ReleaseRuntimeAssets();

        config = gameConfig;
        textureSize = gameConfig.ScratchMaskTextureSize;
        brushRadius = gameConfig.ScratchBrushRadius;
        completionRatio = gameConfig.ScratchCompleteRatio;
        penOffset = gameConfig.ScratchPenOffset;
        isSpecialDraft = isSpecial;
        onScratchCompleted = completed;
        sparkleEffect.Play(gameConfig, isSpecial);
        RefreshPenStarColor(gameConfig.SparkleNormalColor);
        scratchedPixelCount = 0;
        isPointerDown = false;
        isPointerInside = false;
        hasLastScratchPixel = false;
        isScratchActive = true;

        scratchPixels = new Color32[textureSize * textureSize];
        Array.Fill(scratchPixels, Color.white);
        scratchMaskTex = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "MachiRoomScratchMask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        scratchMaskTex.SetPixels32(scratchPixels);
        scratchMaskTex.Apply(false, false);

        scratchMaterial = new Material(scratchShader)
        {
            name = "MachiRoomScratchMaskRuntime",
        };
        scratchMaterial.SetTexture(ScratchMaskProperty, scratchMaskTex);
        Vector4 outerUv = DataUtility.GetOuterUV(maskImage.sprite);
        scratchMaterial.SetVector(SpriteUvRectProperty, outerUv);
        maskImage.material = scratchMaterial;
        maskImage.SetMaterialDirty();
        penRtf.gameObject.SetActive(false);
    }

    public void StopScratch()
    {
        isScratchActive = false;
        isPointerDown = false;
        isPointerInside = false;
        hasLastScratchPixel = false;
        onScratchCompleted = null;
        penRtf.gameObject.SetActive(false);
        sparkleEffect.Stop();
        ReleaseRuntimeAssets();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isScratchActive)
            return;

        isPointerInside = true;
        MovePen(eventData);
        penRtf.gameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        sparkleEffect.ResetTrail();
        if (!isPointerDown)
            penRtf.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isScratchActive || !IsPointerInScratchArea(eventData))
            return;

        isPointerDown = true;
        hasLastScratchPixel = false;
        MovePen(eventData);
        penRtf.gameObject.SetActive(true);
        Scratch(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        hasLastScratchPixel = false;
        if (!isPointerInside)
            penRtf.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isScratchActive || !isSpecialDraft)
            return;

        RefreshPenStarColor(sparkleEffect.CurrentColor);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!isScratchActive || !IsPointerInScratchArea(eventData))
            return;

        MovePen(eventData);
        if (isPointerDown)
            Scratch(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isScratchActive)
            return;

        MovePen(eventData);
        if (IsPointerInScratchArea(eventData))
            Scratch(eventData);
        else
            hasLastScratchPixel = false;
    }

    void OnDisable()
    {
        StopScratch();
    }

    void OnDestroy()
    {
        ReleaseRuntimeAssets();
    }

    void MovePen(PointerEventData eventData)
    {
        RectTransform penParentRtf = (RectTransform)penRtf.parent;

        Camera eventCamera = eventData.enterEventCamera != null
            ? eventData.enterEventCamera
            : eventData.pressEventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                penParentRtf,
                eventData.position,
                eventCamera,
                out Vector2 localPoint))
            penRtf.anchoredPosition = localPoint + penOffset;

        TrySpawnSparkle(eventData, eventCamera);
    }

    #region Sparkle

    /// <summary>笔移动一段距离就撒一枚闪光，按下作画时更密。</summary>
    void TrySpawnSparkle(PointerEventData eventData, Camera eventCamera)
    {
        sparkleEffect.TryEmitTrail(
            eventData.position,
            eventCamera,
            isPointerDown ? config.SparkleSpawnDistance : config.SparkleSpawnDistance * 2f);
    }

    void RefreshPenStarColor(Color color)
    {
        for (int i = 0; i < penStars.Length; i++)
            penStars[i].color = color;
    }

    #endregion

    bool IsPointerInScratchArea(PointerEventData eventData)
    {
        Camera eventCamera = eventData.enterEventCamera != null
            ? eventData.enterEventCamera
            : eventData.pressEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(
            scratchAreaRtf,
            eventData.position,
            eventCamera);
    }

    void Scratch(PointerEventData eventData)
    {
        Camera eventCamera = eventData.enterEventCamera != null
            ? eventData.enterEventCamera
            : eventData.pressEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                scratchAreaRtf,
                eventData.position,
                eventCamera,
                out Vector2 localPoint))
            return;

        Rect rect = scratchAreaRtf.rect;
        Vector2 pixel = new(
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x) * (textureSize - 1),
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y) * (textureSize - 1));

        bool changed = hasLastScratchPixel
            ? ScratchSegment(lastScratchPixel, pixel, rect)
            : ScratchPoint(pixel, rect);
        lastScratchPixel = pixel;
        hasLastScratchPixel = true;

        if (!changed)
            return;

        scratchMaskTex.SetPixels32(scratchPixels);
        scratchMaskTex.Apply(false, false);
        if ((float)scratchedPixelCount / scratchPixels.Length >= completionRatio)
            EndScratch();
    }

    bool ScratchSegment(Vector2 from, Vector2 to, Rect rect)
    {
        float radiusInTexture = brushRadius / Mathf.Min(rect.width, rect.height) * textureSize;
        int stepCount = Mathf.Max(
            1,
            Mathf.CeilToInt(Vector2.Distance(from, to) / Mathf.Max(1f, radiusInTexture * 0.4f)));
        bool changed = false;
        for (int i = 1; i <= stepCount; i++)
            changed |= ScratchPoint(Vector2.Lerp(from, to, (float)i / stepCount), rect);
        return changed;
    }

    bool ScratchPoint(Vector2 center, Rect rect)
    {
        int radiusX = Mathf.Max(
            1,
            Mathf.CeilToInt(brushRadius / rect.width * textureSize));
        int radiusY = Mathf.Max(
            1,
            Mathf.CeilToInt(brushRadius / rect.height * textureSize));
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x) - radiusX);
        int maxX = Mathf.Min(textureSize - 1, Mathf.CeilToInt(center.x) + radiusX);
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y) - radiusY);
        int maxY = Mathf.Min(textureSize - 1, Mathf.CeilToInt(center.y) + radiusY);
        bool changed = false;

        for (int y = minY; y <= maxY; y++)
        {
            float offsetY = (y - center.y) / radiusY;
            for (int x = minX; x <= maxX; x++)
            {
                float offsetX = (x - center.x) / radiusX;
                if (offsetX * offsetX + offsetY * offsetY > 1f)
                    continue;

                int pixelIndex = y * textureSize + x;
                if (scratchPixels[pixelIndex].r == 0)
                    continue;

                scratchPixels[pixelIndex] = Color.clear;
                scratchedPixelCount++;
                changed = true;
            }
        }

        return changed;
    }

    void EndScratch()
    {
        isScratchActive = false;
        isPointerDown = false;
        penRtf.gameObject.SetActive(false);
        Action completed = onScratchCompleted;
        onScratchCompleted = null;
        completed?.Invoke();
    }

    void ReleaseRuntimeAssets()
    {
        if (maskImage != null && maskImage.material == scratchMaterial)
            maskImage.material = null;
        if (scratchMaterial != null)
            Destroy(scratchMaterial);
        if (scratchMaskTex != null)
            Destroy(scratchMaskTex);

        scratchMaterial = null;
        scratchMaskTex = null;
        scratchPixels = null;
    }
}
