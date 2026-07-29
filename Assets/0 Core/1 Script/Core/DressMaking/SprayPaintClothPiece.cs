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
        [Tooltip("XFramework/UI/AlphaTint 材质，上色时启用，否则灰度底图会把设定色乘暗")]
        [SerializeField] Material tintMaterial;
        [Tooltip("底图主色调（0~1），上色时按它归一化。留 0 由编辑器自动从贴图取众数")]
        [SerializeField, Range(0f, 1f)] float baseLevel;

        static readonly int BaseLevelId = Shader.PropertyToID("_BaseLevel");

        int index = -1;
        Action<int> clickCallback;
        Material originMaterial;
        Material tintInstance;

        public bool IsResolved { get; private set; }
        public bool IsCorrect { get; private set; }

        void Awake()
        {
            if (pieceImage != null)
            {
                originMaterial = pieceImage.material;
            }

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
            SetColor(unpaintedColor, false);
        }

        public void ShowResult(bool isCorrect, Color paintColor)
        {
            IsResolved = true;
            IsCorrect = isCorrect;
            SetColor(paintColor, true);
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

        void SetColor(Color color, bool painted)
        {
            if (pieceImage == null)
            {
                return;
            }

            // 底图是灰度稿，直接乘 color 会压暗（104 灰 × 86 = 35）；
            // 上色时切到 AlphaTint 材质，灰度先按主色调归一化，主体区即纯设定色。
            if (tintMaterial != null)
            {
                pieceImage.material = painted ? GetTintInstance() : originMaterial;
            }

            pieceImage.color = color;
        }

        Material GetTintInstance()
        {
            if (tintInstance == null)
            {
                // 每片主色调不同，材质不能共用
                tintInstance = new Material(tintMaterial);
                tintInstance.SetFloat(BaseLevelId, baseLevel > 0f ? baseLevel : 1f);
            }

            return tintInstance;
        }

        void OnDestroy()
        {
            if (tintInstance != null)
            {
                Destroy(tintInstance);
                tintInstance = null;
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

#if UNITY_EDITOR
        void OnValidate()
        {
            if (baseLevel <= 0f)
            {
                baseLevel = DetectBaseLevel();
            }
        }

        /// <summary>
        /// 取不透明像素亮度的众数作为主色调：底图大面积平涂那块就是主体色。
        /// </summary>
        float DetectBaseLevel()
        {
            if (pieceImage == null || pieceImage.sprite == null)
            {
                return 0f;
            }

            var texture = pieceImage.sprite.texture;
            if (texture == null || !texture.isReadable)
            {
                return 0f;
            }

            var pixels = texture.GetPixels32();
            var histogram = new int[256];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                var pixel = pixels[i];
                if (pixel.a < 200)
                {
                    continue;
                }

                histogram[Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b))]++;
            }

            int mode = 0;
            for (int i = 1; i < histogram.Length; i++)
            {
                if (histogram[i] > histogram[mode])
                {
                    mode = i;
                }
            }

            return mode > 0 ? mode / 255f : 0f;
        }
#endif
    }
}
