using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public partial class PuzzleSlot : UIBase,IBeginDragHandler,IDragHandler,IEndDragHandler
{
    private Image _image;
    private RectTransform _rect;

    private PuzzleUI ParentUI;
    /// <summary>自己所属的格子(zhi),拖拽结束后始终回到这里</summary>
    private RectTransform HomeCell;
    /// <summary>拖拽时临时挂载的层级,保证拼图块显示在其它格子之上</summary>
    private RectTransform DragLayer;
    private Vector2 DragOffset;
    private bool IsDragging;

    /// <summary>格子序号,即这一格正确应该放的拼图序号</summary>
    public int CellIndex { get; private set; } = -1;
    /// <summary>当前显示的拼图序号</summary>
    public int PieceIndex { get; private set; } = -1;
    public Sprite PieceSprite => _image != null ? _image.sprite : null;

    public override void Init()
    {
        InitAutoBind();

        _image = GetComponent<Image>();
        _rect = transform as RectTransform;
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(PuzzleUI ui, int cellIndex, RectTransform cell)
    {
        ParentUI = ui;
        CellIndex = cellIndex;
        HomeCell = cell;
        DragLayer = ui != null ? ui.DragLayer : cell.parent as RectTransform;
        IsDragging = false;
        ResetToHome();
    }

    public void SetPiece(int pieceIndex, Sprite sprite)
    {
        PieceIndex = pieceIndex;
        if (_image != null)
        {
            _image.sprite = sprite;
            _image.enabled = sprite != null;
        }
    }

    /// <summary>
    /// 回到自己的格子并铺满格子(对象池取出的实例可能残留上次的位置/缩放,这里统一重置)
    /// </summary>
    private void ResetToHome()
    {
        if (_rect == null || HomeCell == null)
        {
            return;
        }

        if (_rect.parent != HomeCell)
        {
            _rect.SetParent(HomeCell, false);
        }

        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = HomeCell.rect.size;
        _rect.localScale = Vector3.one;
        _rect.localRotation = Quaternion.identity;
        _rect.anchoredPosition = Vector2.zero;

        if (_image != null)
        {
            _image.raycastTarget = true;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ParentUI == null || _rect == null || DragLayer == null)
        {
            return;
        }

        IsDragging = true;
        // 关掉自身射线,拖拽结束时才能检测到下方的拼图块
        if (_image != null)
        {
            _image.raycastTarget = false;
        }

        _rect.SetParent(DragLayer, true);
        _rect.SetAsLastSibling();

        // 记录按下点与拼图中心的偏移,避免图片瞬间跳到指针中心
        DragOffset = Vector2.zero;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(DragLayer, eventData.position, eventData.pressEventCamera, out var localPoint))
        {
            DragOffset = _rect.anchoredPosition - localPoint;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsDragging)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(DragLayer, eventData.position, eventData.pressEventCamera, out var localPoint))
        {
            _rect.anchoredPosition = localPoint + DragOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsDragging)
        {
            return;
        }

        IsDragging = false;
        var target = ParentUI != null ? ParentUI.FindSlotUnderPointer(eventData, this) : null;
        // 交换的是图片内容,拼图块本身始终回到自己的格子
        ResetToHome();
        if (target != null)
        {
            ParentUI.TrySwapPiece(this, target);
        }
    }

    private void OnDisable()
    {
        if (IsDragging)
        {
            IsDragging = false;
            ResetToHome();
        }
    }
}
