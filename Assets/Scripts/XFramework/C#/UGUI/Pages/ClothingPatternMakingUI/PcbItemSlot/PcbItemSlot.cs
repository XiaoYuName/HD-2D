using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

public partial class PcbItemSlot : UIBase,IPointerClickHandler
{
    public RectTransform Rect { get; private set; }
    private CanvasGroup canvasGroup;
    private readonly List<Vector2> physicsShapeBuffer = new List<Vector2>();
    private ClothingPatternMakingUI ParentUI;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void SetData(PcbSlotData data)
    {
        ParentUI = UISystem.Instance.GetUI<ClothingPatternMakingUI>("ClothingPatternMakingUI");
        image.sprite = LoadAsset<Sprite>(GamePathTools.CombinationPcbIconPath(data.MaxIconName));
        image.SetNativeSize();
    }

    public void SetBlocksRaycasts(bool value)
    {
        canvasGroup.blocksRaycasts = value;
    }

    public void SetColor(Color color)
    {
        image.color = color;
    }

    public List<Vector2[]> GetShapePolygonsRelativeTo(RectTransform target)
    {
        var polygons = new List<Vector2[]>();
        if (target == null)
        {
            return polygons;
        }

        var sprite = image.sprite;
        if (sprite != null && sprite.GetPhysicsShapeCount() > 0)
        {
            for (var i = 0; i < sprite.GetPhysicsShapeCount(); i++)
            {
                physicsShapeBuffer.Clear();
                sprite.GetPhysicsShape(i, physicsShapeBuffer);
                if (physicsShapeBuffer.Count < 3)
                {
                    continue;
                }

                var points = new Vector2[physicsShapeBuffer.Count];
                for (var j = 0; j < physicsShapeBuffer.Count; j++)
                {
                    points[j] = SpritePointToTargetLocal(physicsShapeBuffer[j], target);
                }
                polygons.Add(points);
            }
        }

        if (polygons.Count == 0)
        {
            var corners = new Vector3[4];
            Rect.GetWorldCorners(corners);
            var points = new Vector2[corners.Length];
            for (var i = 0; i < corners.Length; i++)
            {
                points[i] = target.InverseTransformPoint(corners[i]);
            }
            polygons.Add(points);
        }

        return polygons;
    }

    private Vector2 SpritePointToTargetLocal(Vector2 spritePoint, RectTransform target)
    {
        var sprite = image.sprite;
        var bounds = sprite.bounds;
        var rect = Rect.rect;

        var normalizedX = Mathf.InverseLerp(bounds.min.x, bounds.max.x, spritePoint.x);
        var normalizedY = Mathf.InverseLerp(bounds.min.y, bounds.max.y, spritePoint.y);
        var rectLocalPoint = new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedX),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedY)
        );

        return target.InverseTransformPoint(Rect.TransformPoint(rectLocalPoint));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ParentUI.ShowEditorGroup(this,Rect.anchoredPosition);
    }
}
