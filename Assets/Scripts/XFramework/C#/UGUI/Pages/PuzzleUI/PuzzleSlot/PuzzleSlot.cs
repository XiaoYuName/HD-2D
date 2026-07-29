using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public partial class PuzzleSlot : UIBase,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerDownHandler,IPointerUpHandler
{
    /// <summary>一圈分成几档朝向,即 90° 一档</summary>
    public const int RotationStepCount = 4;

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
    /// <summary>当前朝向,0~3 每档 90°,只有 0 才算摆正</summary>
    public int RotationStep { get; private set; }
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
        RotationStep = 0;
        ResetToHome();
    }

    public void SetPiece(int pieceIndex, Sprite sprite, int rotationStep)
    {
        PieceIndex = pieceIndex;
        // 负数取模也要落在 0~3
        RotationStep = ((rotationStep % RotationStepCount) + RotationStepCount) % RotationStepCount;

        if (_image != null)
        {
            _image.sprite = sprite;
            _image.enabled = sprite != null;
        }
        ApplyRotation();
    }

    /// <summary>按空格时调用,转 +90°</summary>
    public void Rotate()
    {
        RotationStep = (RotationStep + 1) % RotationStepCount;
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        if (_rect != null)
        {
            _rect.localRotation = Quaternion.Euler(0f, 0f, 90f * RotationStep);
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
        _rect.anchoredPosition = Vector2.zero;
        // 归位不能把朝向也清掉,朝向是拼图块自己的状态
        ApplyRotation();

        if (_image != null)
        {
            _image.raycastTarget = true;
        }
    }

    // 按住期间按空格转的就是这一块,由面板统一记录当前按住的是谁
    public void OnPointerDown(PointerEventData eventData)
    {
        ParentUI?.SetPressedSlot(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ParentUI?.ClearPressedSlot(this);
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
        // 交换的是图片内容和朝向,拼图块本身始终回到自己的格子
        ResetToHome();
        if (target != null)
        {
            ParentUI.TrySwapPiece(this, target);
        }
    }

    private void OnDisable()
    {
        ParentUI?.ClearPressedSlot(this);

        if (IsDragging)
        {
            IsDragging = false;
            ResetToHome();
        }
    }
}
