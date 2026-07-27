using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public partial class SewingMachineSlot : UIBase, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [LabelText("类型")]
    public ParentType ParentType;

    [LabelText("吸附动画时长")]
    public float SnapDuration = 0.18f;

    [LabelText("吸附动画曲线")]
    public Ease SnapEase = Ease.OutBack;

    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private Camera eventCamera;
    private Vector3 dragOffset;
    private bool isDragging;
    private bool isSnapping;
    private bool isSnapped;
    private SewingMachineSlotParent snappedParent;
    private Graphic[] graphics;
    private bool[] originalRaycastTargets;
    private Sequence snapSequence;
    private Action<SewingMachineSlot> snappedCallback;
    private List<SewingMachineSlotParent> snapParents = new List<SewingMachineSlotParent>();
    private readonly Vector3[] slotCorners = new Vector3[4];
    private readonly Vector3[] parentCorners = new Vector3[4];
    private readonly List<Vector2> slotPolygon = new List<Vector2>(4);
    private readonly List<Vector2> parentPolygon = new List<Vector2>(4);
    private readonly List<Vector2> clippedPolygon = new List<Vector2>(8);

    public bool IsSnapped => isSnapped;
    public SewingMachineSlotParent SnappedParent => snappedParent;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
        CacheGraphics();
        SetGraphicRaycastTargets(true);
    }

    public void SetSnappedCallback(Action<SewingMachineSlot> callback)
    {
        snappedCallback = callback;
    }

    public void SetSnapParents(IEnumerable<SewingMachineSlotParent> parents)
    {
        snapParents.Clear();
        if (parents == null)
        {
            return;
        }

        foreach (SewingMachineSlotParent parent in parents)
        {
            if (parent != null)
            {
                snapParents.Add(parent);
            }
        }
    }

    public override void Release()
    {
        KillSnapTween();
        snappedCallback = null;
        base.Release();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || isSnapping || isSnapped)
        {
            return;
        }

        KillSnapTween();
        isDragging = true;
        eventCamera = eventData.pressEventCamera;
        rectTransform.SetAsLastSibling();

        if (TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
        {
            dragOffset = rectTransform.position - worldPosition;
            rectTransform.position = worldPosition + dragOffset;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || isSnapping || isSnapped)
        {
            return;
        }

        if (TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
        {
            rectTransform.position = worldPosition + dragOffset;
        }

        SewingMachineSlotParent target = FindSnapTarget();
        if (target != null)
        {
            SnapTo(target);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
    }

    private bool TryGetPointerWorldPosition(PointerEventData eventData, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        RectTransform plane = rootCanvas != null
            ? (RectTransform)rootCanvas.transform
            : rectTransform;

        Camera camera = rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : eventData.pressEventCamera;

        return RectTransformUtility.ScreenPointToWorldPointInRectangle(
            plane,
            eventData.position,
            camera,
            out worldPosition);
    }

    private SewingMachineSlotParent FindSnapTarget()
    {
        SewingMachineSlotParent bestTarget = null;
        float bestRatio = 0f;

        for (int i = 0; i < snapParents.Count; i++)
        {
            SewingMachineSlotParent target = snapParents[i];
            if (target == null || target.ParentType != ParentType)
            {
                continue;
            }

            RectTransform targetRect = target.transform as RectTransform;
            if (targetRect == null)
            {
                continue;
            }

            float overlapRatio = GetOverlapRatio(rectTransform, targetRect);
            if (overlapRatio >= target.SnapOverlapRatio && overlapRatio > bestRatio)
            {
                bestRatio = overlapRatio;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    private float GetOverlapRatio(RectTransform slotRect, RectTransform targetRect)
    {
        BuildPolygon(slotRect, slotCorners, slotPolygon);
        BuildPolygon(targetRect, parentCorners, parentPolygon);

        float parentArea = Mathf.Abs(GetPolygonArea(parentPolygon));
        if (parentArea <= 0.001f)
        {
            return 0f;
        }

        ClipPolygon(slotPolygon, parentPolygon, clippedPolygon);
        if (clippedPolygon.Count < 3)
        {
            return 0f;
        }

        float overlapArea = Mathf.Abs(GetPolygonArea(clippedPolygon));
        return overlapArea / parentArea;
    }

    private void BuildPolygon(RectTransform source, Vector3[] corners, List<Vector2> polygon)
    {
        source.GetWorldCorners(corners);
        polygon.Clear();
        for (int i = 0; i < corners.Length; i++)
        {
            polygon.Add(corners[i]);
        }
    }

    private float GetPolygonArea(List<Vector2> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            area += current.x * next.y - next.x * current.y;
        }

        return area * 0.5f;
    }

    private void ClipPolygon(List<Vector2> subject, List<Vector2> clip, List<Vector2> output)
    {
        output.Clear();
        for (int i = 0; i < subject.Count; i++)
        {
            output.Add(subject[i]);
        }

        float clipDirectionSign = GetPolygonArea(clip) >= 0f ? 1f : -1f;
        List<Vector2> input = new List<Vector2>(8);
        for (int i = 0; i < clip.Count; i++)
        {
            input.Clear();
            for (int j = 0; j < output.Count; j++)
            {
                input.Add(output[j]);
            }

            output.Clear();
            if (input.Count == 0)
            {
                break;
            }

            Vector2 clipA = clip[i];
            Vector2 clipB = clip[(i + 1) % clip.Count];
            Vector2 previous = input[input.Count - 1];

            for (int j = 0; j < input.Count; j++)
            {
                Vector2 current = input[j];
                bool currentInside = IsInsideClipEdge(current, clipA, clipB, clipDirectionSign);
                bool previousInside = IsInsideClipEdge(previous, clipA, clipB, clipDirectionSign);

                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output.Add(GetLineIntersection(previous, current, clipA, clipB));
                    }

                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(GetLineIntersection(previous, current, clipA, clipB));
                }

                previous = current;
            }
        }
    }

    private bool IsInsideClipEdge(Vector2 point, Vector2 edgeA, Vector2 edgeB, float clipDirectionSign)
    {
        Vector2 edge = edgeB - edgeA;
        Vector2 toPoint = point - edgeA;
        float cross = edge.x * toPoint.y - edge.y * toPoint.x;
        return cross * clipDirectionSign >= -0.001f;
    }

    private Vector2 GetLineIntersection(Vector2 subjectA, Vector2 subjectB, Vector2 clipA, Vector2 clipB)
    {
        Vector2 subjectDirection = subjectB - subjectA;
        Vector2 clipDirection = clipB - clipA;
        float denominator = subjectDirection.x * clipDirection.y - subjectDirection.y * clipDirection.x;
        if (Mathf.Abs(denominator) <= 0.001f)
        {
            return subjectB;
        }

        Vector2 difference = clipA - subjectA;
        float t = (difference.x * clipDirection.y - difference.y * clipDirection.x) / denominator;
        return subjectA + subjectDirection * t;
    }

    private void SnapTo(SewingMachineSlotParent target)
    {
        if (target == null)
        {
            return;
        }

        KillSnapTween();
        isDragging = false;
        isSnapping = true;

        snapSequence = DOTween.Sequence();
        snapSequence.Join(rectTransform.DOMove(target.transform.position, SnapDuration));
        snapSequence.Join(rectTransform.DORotateQuaternion(target.transform.rotation, SnapDuration));
        snapSequence.SetEase(SnapEase);
        snapSequence.OnComplete(() =>
        {
            rectTransform.position = target.transform.position;
            rectTransform.rotation = target.transform.rotation;
            rectTransform.SetAsFirstSibling();
            SetGraphicRaycastTargets(false);
            snappedParent = target;
            isSnapped = true;
            isSnapping = false;
            snappedCallback?.Invoke(this);
        });
    }

    public bool StartScratch(Camera camera)
    {
        if (!isSnapped || snappedParent == null)
        {
            return false;
        }

        return snappedParent.StartScratch(camera, this);
    }

    private void CacheGraphics()
    {
        graphics = GetComponentsInChildren<Graphic>(true);
        originalRaycastTargets = new bool[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
        {
            originalRaycastTargets[i] = graphics[i] != null && graphics[i].raycastTarget;
        }
    }

    private void SetGraphicRaycastTargets(bool enabled)
    {
        if (graphics == null || originalRaycastTargets == null)
        {
            CacheGraphics();
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = enabled && originalRaycastTargets[i];
            }
        }
    }

    private void KillSnapTween()
    {
        snapSequence?.Kill();
        snapSequence = null;
        isSnapping = false;
    }
}
