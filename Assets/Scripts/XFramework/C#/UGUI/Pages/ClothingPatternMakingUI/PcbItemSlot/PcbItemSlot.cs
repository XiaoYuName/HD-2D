using System;
using System.Collections.Generic;
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

public partial class PcbItemSlot : UIBase, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PcbSlotData Data { get; private set; }
    public RectTransform Rect { get; private set; }
    private CanvasGroup canvasGroup;
    private readonly List<Vector2> physicsShapeBuffer = new List<Vector2>();
    private ClothingPatternMakingUI ParentUI;
    private bool isSelected;
    private bool isDragging;
    private bool isRotating;
    private int ignoreClickUntilFrame;
    private Vector2 dragStartPosition;
    private Vector2 dragPointerOffset;
    private float rotateStartAngle;
    private float rotateStartPointerAngle;
    private UIEffect uiEffect;
    private CanvasGroup rotationCanvasGroup;
    private bool isPointerEnter;

    public override void Init()
    {
        InitAutoBind();
        isSelected = false;
        isDragging = false;
        isRotating = false;
        ignoreClickUntilFrame = -1;
        uiEffect = GetComponent<UIEffect>();
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        if (rotation != null)
        {
            rotationCanvasGroup = rotation.GetComponent<CanvasGroup>();
            if (rotationCanvasGroup == null)
            {
                rotationCanvasGroup = rotation.gameObject.AddComponent<CanvasGroup>();
            }
        }
        RegisterRotationEvents();
        RefreshRotationControlState();
    }

    private void OnEnable()
    {
        PlayerInputManager.Instance.OnRightClick += OnRightClick;
    }

    private void OnDisable()
    {
        PlayerInputManager.Instance.OnRightClick -= OnRightClick;
    }

    public void SetData(PcbSlotData data)
    {
        Data = data;
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
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (Time.frameCount <= ignoreClickUntilFrame)
        {
            return;
        }

        if (isSelected)
        {
            ParentUI.DeselectPcbItemSlot(this);
        }
        else
        {
            ParentUI.SelectPcbItemSlot(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerEnter = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerEnter = false;
    }

    public void SetSelected(bool value)
    {
        isSelected = value;
        if (!isSelected)
        {
            isDragging = false;
            isRotating = false;
            SetColor(Color.white);
            SetBlocksRaycasts(true);
        }

        if (uiEffect != null)
        {
            uiEffect.edgeMode = isSelected ? EdgeMode.Plain : EdgeMode.None;
        }
        RefreshRotationControlState();
    }

    private void OnRightClick()
    {
        if (!isSelected || !isPointerEnter)
        {
            return;
        }

        Debug.Log("进行删除操作!");
        ParentUI.DeletePcbItemSlot(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isSelected || isRotating || IsPointerFromRotationControl(eventData))
        {
            return;
        }

        isDragging = true;
        dragStartPosition = Rect.anchoredPosition;
        dragPointerOffset = GetDragPointerOffset(eventData);
        Rect.SetAsLastSibling();
        RefreshRotationControlState();
        SetBlocksRaycasts(false);
        MoveToPointerWithOffset(eventData);
        ParentUI.UpdatePcbItemSlotDragColor(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || isRotating)
        {
            return;
        }

        MoveToPointerWithOffset(eventData);
        ParentUI.UpdatePcbItemSlotDragColor(this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || isRotating)
        {
            return;
        }

        isDragging = false;
        MoveToPointerWithOffset(eventData);
        if (!ParentUI.CanPlacePcbItemSlot(this))
        {
            Rect.anchoredPosition = dragStartPosition;
        }

        SetColor(Color.white);
        SetBlocksRaycasts(true);
        IgnoreClickBriefly();
        RefreshRotationControlState();
    }

    private void RegisterRotationEvents()
    {
        if (rotation == null)
        {
            return;
        }

        rotation.triggers.Clear();
        AddRotationEvent(EventTriggerType.PointerDown, OnRotationPointerDown);
        AddRotationEvent(EventTriggerType.Drag, OnRotationDrag);
        AddRotationEvent(EventTriggerType.PointerUp, OnRotationPointerUp);
    }

    private void AddRotationEvent(EventTriggerType eventType, Action<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(data => callback?.Invoke(data));
        rotation.triggers.Add(entry);
    }

    private void OnRotationPointerDown(BaseEventData data)
    {
        var pointerEventData = data as PointerEventData;
        if (!isSelected || pointerEventData == null)
        {
            return;
        }

        isRotating = true;
        isDragging = false;
        rotateStartAngle = Rect.localEulerAngles.z;
        rotateStartPointerAngle = GetPointerAngle(pointerEventData);
        Rect.SetAsLastSibling();
        RefreshRotationControlState();
        ParentUI.UpdatePcbItemSlotDragColor(this);
        pointerEventData.Use();
    }

    private void OnRotationDrag(BaseEventData data)
    {
        var pointerEventData = data as PointerEventData;
        if (!isRotating || pointerEventData == null)
        {
            return;
        }

        var currentPointerAngle = GetPointerAngle(pointerEventData);
        var deltaAngle = Mathf.DeltaAngle(rotateStartPointerAngle, currentPointerAngle);
        Rect.localEulerAngles = new Vector3(0f, 0f, rotateStartAngle + deltaAngle);
        ParentUI.UpdatePcbItemSlotDragColor(this);
        pointerEventData.Use();
    }

    private void OnRotationPointerUp(BaseEventData data)
    {
        if (!isRotating)
        {
            return;
        }

        isRotating = false;
        if (!ParentUI.CanPlacePcbItemSlot(this))
        {
            Rect.localEulerAngles = new Vector3(0f, 0f, rotateStartAngle);
        }

        SetColor(Color.white);
        IgnoreClickBriefly();
        ParentUI.DeselectPcbItemSlot(this);
        var pointerEventData = data as PointerEventData;
        if (pointerEventData != null)
        {
            pointerEventData.Use();
        }
    }

    private Vector2 GetDragPointerOffset(PointerEventData eventData)
    {
        var parent = Rect.parent as RectTransform;
        if (parent == null)
        {
            return Vector2.zero;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var localPosition))
        {
            return Rect.anchoredPosition - localPosition;
        }

        return Vector2.zero;
    }

    private void MoveToPointerWithOffset(PointerEventData eventData)
    {
        var parent = Rect.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var localPosition))
        {
            Rect.anchoredPosition = localPosition + dragPointerOffset;
        }
    }

    private void IgnoreClickBriefly()
    {
        ignoreClickUntilFrame = Time.frameCount + 1;
    }

    private float GetPointerAngle(PointerEventData eventData)
    {
        var parent = Rect.parent as RectTransform;
        if (parent == null)
        {
            return rotateStartPointerAngle;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var localPosition))
        {
            return rotateStartPointerAngle;
        }

        var direction = localPosition - Rect.anchoredPosition;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return rotateStartPointerAngle;
        }

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private void RefreshRotationControlState()
    {
        if (rotation != null)
        {
            rotation.gameObject.SetActive(isSelected && !isDragging);
        }

        if (rotationCanvasGroup != null)
        {
            rotationCanvasGroup.alpha = isRotating ? 0f : 1f;
            rotationCanvasGroup.blocksRaycasts = true;
            rotationCanvasGroup.interactable = true;
        }

        if (rotationFarme != null)
        {
            rotationFarme.gameObject.SetActive(isSelected && isRotating);
        }
    }

    private bool IsPointerFromRotationControl(PointerEventData eventData)
    {
        if (rotation == null || eventData == null)
        {
            return false;
        }

        var rotationTransform = rotation.transform;
        return IsChildOf(eventData.pointerPress, rotationTransform)
               || IsChildOf(eventData.pointerEnter, rotationTransform);
    }

    private bool IsChildOf(GameObject obj, Transform parent)
    {
        return obj != null && parent != null && obj.transform.IsChildOf(parent);
    }
}
