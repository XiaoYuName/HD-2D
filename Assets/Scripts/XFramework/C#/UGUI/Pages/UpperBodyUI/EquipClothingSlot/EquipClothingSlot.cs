using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public partial class EquipClothingSlot : UIBase, IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter
{
    public ClothingAccessoriesData Data { get; private set; }
    public RectTransform Rect { get; private set; }

    /// <summary>
    /// 这个配件的中心点(自身本地坐标),取素材在 Sprite Editor 里设的 pivot。
    /// 素材是整张人物画布大小、配件只占其中一小块,所以 Rect 中心根本不是玩家看到的位置,
    /// 摆放、跟随鼠标、落点判定一律以这个点为基准。
    /// 也就是说配件摆在哪由美术改 pivot 决定,不用回代码调偏移。
    /// </summary>
    public Vector2 PivotLocalPosition { get; private set; }

    private Image iconImage;
    private CanvasGroup canvasGroup;
    private UpperBodyUI ParentUI;
    private Vector2 homePosition;
    private bool isDragging;

    /// <summary>
    /// 实际图形的三角面(自身本地坐标),命中判定用。只跟素材和尺寸有关,SetData 时算一次就够。
    /// </summary>
    private Vector2[] shapeTriangles;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        iconImage = GetComponent<Image>();
        Rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        isDragging = false;
    }

    public void SetData(ClothingAccessoriesData accessoriesData)
    {
        Data = accessoriesData;
        iconImage.sprite = LoadAsset<Sprite>(GamePathTools.CombinationAccessoriesIconPath(accessoriesData.AccessoriesMaxIconName));
        iconImage.SetNativeSize();
        // 命中判定按实际图形(Tight 网格)来,不然整张画布的透明留白也会吃掉点击
        shapeTriangles = UISpriteShapeUtils.GetShapeTrianglesLocal(iconImage, Rect);
        // 定位基准取素材自己的 pivot,美术在 Sprite Editor 里把它打在配件上
        PivotLocalPosition = UISpriteShapeUtils.TryGetSpritePivotLocal(iconImage, Rect, out var pivotLocal)
            ? pivotLocal
            : Rect.rect.center;
        ParentUI = UISystem.Instance.GetUI<UpperBodyUI>(UIKeys.UpperBodyUI);
    }

    /// <summary>
    /// 只有点在实际图形上才算命中。素材四周的透明留白几乎占满整个面板,
    /// 不挡掉的话点面板空白处也能把这件配件拖走。
    /// </summary>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        // 拿不到网格时不拦,否则整件配件都点不动
        if (shapeTriangles == null)
        {
            return true;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screenPoint, eventCamera, out var localPoint))
        {
            return false;
        }

        return UISpriteShapeUtils.IsPointInsideTriangles(shapeTriangles, localPoint);
    }

    public void SetBlocksRaycasts(bool value)
    {
        canvasGroup.blocksRaycasts = value;
    }

    /// <summary>
    /// 配件中心点的世界坐标,用于落点判定
    /// </summary>
    public Vector3 GetPivotWorldPosition()
    {
        return Rect.TransformPoint(PivotLocalPosition);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || ParentUI == null)
        {
            return;
        }

        isDragging = true;
        homePosition = Rect.anchoredPosition;
        Rect.SetAsLastSibling();
        // 拖拽中不挡射线,否则会挡住底下的装配位
        SetBlocksRaycasts(false);
        ParentUI.MoveEquipClothingSlotToScreenPoint(this, eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        ParentUI.MoveEquipClothingSlotToScreenPoint(this, eventData.position, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        ParentUI.MoveEquipClothingSlotToScreenPoint(this, eventData.position, eventData.pressEventCamera);
        SetBlocksRaycasts(true);

        // 装配成功后本对象已被回收,不能再碰它
        if (!ParentUI.TryEquipClothingSlot(this))
        {
            Rect.anchoredPosition = homePosition;
        }
    }
}
