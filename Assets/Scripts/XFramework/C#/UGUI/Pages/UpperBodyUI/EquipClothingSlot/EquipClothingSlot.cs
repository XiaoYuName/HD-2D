using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public partial class EquipClothingSlot : UIBase, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ClothingAccessoriesData Data { get; private set; }
    public RectTransform Rect { get; private set; }

    /// <summary>
    /// 图片实际内容(不含透明留白)的中心,自身本地坐标。
    /// 素材四周常有大片留白,Rect 中心并不是玩家看到的图形中心。
    /// </summary>
    public Vector2 ContentLocalCenter { get; private set; }

    private Image iconImage;
    private CanvasGroup canvasGroup;
    private UpperBodyUI ParentUI;
    private Vector2 homePosition;
    private bool isDragging;

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
        ContentLocalCenter = UISpriteShapeUtils.TryGetContentBounds(iconImage, Rect, Rect, out var contentBounds)
            ? contentBounds.center
            : Rect.rect.center;
        ParentUI = UISystem.Instance.GetUI<UpperBodyUI>(UIKeys.UpperBodyUI);
    }

    public void SetBlocksRaycasts(bool value)
    {
        canvasGroup.blocksRaycasts = value;
    }

    /// <summary>
    /// 图片实际内容的中心(世界坐标),用于落点判定
    /// </summary>
    public Vector3 GetContentWorldCenter()
    {
        return Rect.TransformPoint(ContentLocalCenter);
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
