using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>
    /// 单个服饰片：只有一张黑白底图，喷涂结果靠改 Image.color 表现。
    /// 贴图必须勾 Read/Write Enabled，否则 alphaHitTestMinimumThreshold 会抛异常。
    /// </summary>
    public sealed class SprayPaintClothPiece : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image pieceImage;
        [SerializeField] Color unpaintedColor = Color.white;
        [SerializeField, Range(0f, 1f)] float alphaHitThreshold = 0.1f;

        int index = -1;
        Action<int> clickCallback;

        public bool IsResolved { get; private set; }
        public bool IsCorrect { get; private set; }

        void Awake()
        {
            // 点击热区按黑白图的不透明像素判定：所有片都是整块画布，只有自己那块不透明。
            ApplyAlphaHitTest();
        }

        public void SetData(int value, Action<int> onClick)
        {
            index = value;
            clickCallback = onClick;
            ResetPiece();
        }

        public void ResetPiece()
        {
            IsResolved = false;
            IsCorrect = false;
            SetColor(unpaintedColor);
        }

        public void ShowResult(bool isCorrect, Color paintColor)
        {
            IsResolved = true;
            IsCorrect = isCorrect;
            SetColor(paintColor);
            Tween.PunchScale(transform, Vector3.one * 0.06f, 0.25f);
        }

        public void PlaySelectedFeedback()
        {
            Tween.PunchScale(transform, Vector3.one * 0.04f, 0.2f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsResolved)
            {
                return;
            }

            clickCallback?.Invoke(index);
        }

        void SetColor(Color color)
        {
            if (pieceImage != null)
            {
                pieceImage.color = color;
            }
        }

        void ApplyAlphaHitTest()
        {
            if (pieceImage == null || pieceImage.sprite == null)
            {
                return;
            }

            if (!pieceImage.sprite.texture.isReadable)
            {
                Debug.LogWarning($"[SprayPaint] 贴图 {pieceImage.sprite.texture.name} 未开启 Read/Write Enabled，服饰片 {name} 的点击热区会退化为整块矩形。");
                return;
            }

            pieceImage.alphaHitTestMinimumThreshold = alphaHitThreshold;
        }
    }
}
