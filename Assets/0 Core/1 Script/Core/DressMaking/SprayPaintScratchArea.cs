using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace XFramework
{
    public sealed class SprayPaintScratchArea : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerMoveHandler,
        IDragHandler
    {
        const string ScratchMaskProperty = "_ScratchMask";
        const string SpriteUvRectProperty = "_SpriteUvRect";

        [SerializeField] RectTransform scratchAreaRtf;
        [SerializeField] RectTransform sprayPaintCanRt;
        [SerializeField] Image[] coverImages;
        [SerializeField] Shader scratchShader;
        [SerializeField, Min(64)] int textureSize = 256;
        [SerializeField, Min(1f)] float brushRadius = 42f;
        [SerializeField, Range(0.01f, 1f)] float completeRatio = 0.24f;
        [SerializeField] Vector2 sprayPaintCanOffset = new(22f, -42f);

        Material[] coverMaterials;
        Texture2D scratchMaskTex;
        Color32[] scratchPixels;
        Vector2 lastScratchPixel;
        int scratchedPixelCount;
        Camera uiCamera;
        bool hasLastScratchPixel;
        bool isPointerDown;
        bool isPainting;
        Action completedCallback;

        public void SetCamera(Camera value)
        {
            uiCamera = value;
        }

        public void StartPainting(Action completed)
        {
            StopPainting();
            completedCallback = completed;
            scratchedPixelCount = 0;
            scratchPixels = new Color32[textureSize * textureSize];
            Array.Fill(scratchPixels, Color.white);
            scratchMaskTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, true)
            {
                name = "SprayPaintScratchMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            scratchMaskTex.SetPixels32(scratchPixels);
            scratchMaskTex.Apply(false, false);

            coverMaterials = new Material[coverImages.Length];
            for (int i = 0; i < coverImages.Length; i++)
            {
                Material material = new(scratchShader);
                material.SetTexture(ScratchMaskProperty, scratchMaskTex);
                material.SetVector(SpriteUvRectProperty, DataUtility.GetOuterUV(coverImages[i].sprite));
                coverImages[i].material = material;
                coverMaterials[i] = material;
            }

            isPainting = true;
        }

        public void StopPainting()
        {
            isPainting = false;
            hasLastScratchPixel = false;
            isPointerDown = false;
            completedCallback = null;

            if (coverMaterials != null)
            {
                for (int i = 0; i < coverMaterials.Length; i++)
                {
                    if (coverImages[i].material == coverMaterials[i])
                    {
                        coverImages[i].material = null;
                    }

                    Destroy(coverMaterials[i]);
                }
            }

            Destroy(scratchMaskTex);
            coverMaterials = null;
            scratchMaskTex = null;
            scratchPixels = null;
        }

        void Update()
        {
            FollowSprayPaintCan(Mouse.current.position.ReadValue());
        }

        void OnDestroy()
        {
            StopPainting();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPointerDown = true;
            FollowSprayPaintCan(eventData.position);
            Paint(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerDown = false;
            hasLastScratchPixel = false;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            FollowSprayPaintCan(eventData.position);
            if (isPointerDown)
            {
                Paint(eventData.position);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            FollowSprayPaintCan(eventData.position);
            Paint(eventData.position);
        }

        void FollowSprayPaintCan(Vector2 screenPoint)
        {
            RectTransform parentRtf = (RectTransform)sprayPaintCanRt.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRtf,
                screenPoint,
                uiCamera,
                out Vector2 localPoint);
            sprayPaintCanRt.anchoredPosition = localPoint + sprayPaintCanOffset;
        }

        void Paint(Vector2 screenPoint)
        {
            if (!isPainting)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    scratchAreaRtf,
                    screenPoint,
                    uiCamera,
                    out Vector2 localPoint)
                || !scratchAreaRtf.rect.Contains(localPoint))
            {
                hasLastScratchPixel = false;
                return;
            }

            Rect rect = scratchAreaRtf.rect;
            Vector2 pixel = new(
                Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x) * (textureSize - 1),
                Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y) * (textureSize - 1));
            bool changed = hasLastScratchPixel
                ? PaintSegment(lastScratchPixel, pixel, rect)
                : PaintPoint(pixel, rect);
            lastScratchPixel = pixel;
            hasLastScratchPixel = true;

            if (!changed)
            {
                return;
            }

            scratchMaskTex.SetPixels32(scratchPixels);
            scratchMaskTex.Apply(false, false);
            if ((float)scratchedPixelCount / scratchPixels.Length >= completeRatio)
            {
                EndPainting();
            }
        }

        bool PaintSegment(Vector2 from, Vector2 to, Rect rect)
        {
            float radiusInTexture = brushRadius / Mathf.Min(rect.width, rect.height) * textureSize;
            int stepCount = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / (radiusInTexture * 0.4f)));
            bool changed = false;
            for (int i = 1; i <= stepCount; i++)
            {
                changed |= PaintPoint(Vector2.Lerp(from, to, (float)i / stepCount), rect);
            }

            return changed;
        }

        bool PaintPoint(Vector2 center, Rect rect)
        {
            int radiusX = Mathf.Max(1, Mathf.CeilToInt(brushRadius / rect.width * textureSize));
            int radiusY = Mathf.Max(1, Mathf.CeilToInt(brushRadius / rect.height * textureSize));
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
                    {
                        continue;
                    }

                    int pixelIndex = y * textureSize + x;
                    if (scratchPixels[pixelIndex].r == 0)
                    {
                        continue;
                    }

                    scratchPixels[pixelIndex] = Color.clear;
                    scratchedPixelCount++;
                    changed = true;
                }
            }

            return changed;
        }

        void EndPainting()
        {
            isPainting = false;
            hasLastScratchPixel = false;
            isPointerDown = false;
            Action completed = completedCallback;
            completedCallback = null;
            completed?.Invoke();
        }
    }
}
