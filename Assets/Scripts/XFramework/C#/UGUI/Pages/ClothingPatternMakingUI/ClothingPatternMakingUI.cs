using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using XFramework;

public partial class ClothingPatternMakingUI : UIBase
{
    private static readonly Color ValidDragColor = new Color(0.25f, 1f, 0.25f, 1f);
    private static readonly Color InvalidDragColor = new Color(1f, 0.25f, 0.25f, 1f);

    public ClothingAccessoriesBag CurrentBagData { get; private set; }
    public ClothingAccessoriesData CurrentData { get; private set; }

    private List<PattentSlot> PattentSlotList = new List<PattentSlot>();
    private List<PcbItemSlot> PcbItemSlotList = new List<PcbItemSlot>();
    private PcbItemSlot SelectedPcbItemSlot;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(closeButton,Close,"");
        editorButtonGroups.Init();
        Bind(completeBtn,Complete,"");
        RefreshCompleteButtonState();
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        foreach (var slot in PattentSlotList)
        {
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        PattentSlotList.Clear();
        ClearPcbItemSlots();
        RefreshCompleteButtonState();
    }

    public void SetData(ClothingAccessoriesBag bagData)
    {
        foreach (var slot in PattentSlotList)
        {
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        PattentSlotList.Clear();
        ClearPcbItemSlots();
        
        CurrentBagData = bagData;
        CurrentData = LubanManager.Instance.TbClothingAccessoriesData.Get(bagData.accessoriesID);
        nameStr.SetText(CurrentData.AccessoriesName);
        descStr.SetText(CurrentData.AccessoriesDesc);
        icon.sprite = LoadAsset<Sprite>(GamePathTools.CombinationAccessoriesIconPath(CurrentData.AccessoriesIconName));
        foreach (var PcbSlotID in CurrentData.PcbSlotList)
        {
            var PcbData = LubanManager.Instance.TbPcbSlotData.Get(PcbSlotID);
            SpawnPattentSlot(PcbData);
        }
        RefreshCompleteButtonState();
    }

    public void ShowEditorGroup(PcbItemSlot pcbItemSlot,Vector2 screenPosition)
    {
        editorButtonGroups.CanvasGroup.DOFade(1, 0.25f);
        editorButtonGroups.CanvasGroup.blocksRaycasts = true;
        editorButtonGroups.Rect.anchoredPosition = screenPosition;
        editorButtonGroups.SetData(pcbItemSlot);
    }
    

    public PcbItemSlot SpawnPcbItemSlot(PcbSlotData data, Vector2 screenPosition, Camera eventCamera)
    {
        var obj = AssetsManager.Instance.Instantiate(AssetKeys.PcbItemSlotPath);
        obj.transform.SetParent(pCB, false);
        obj.transform.localScale = Vector3.one;
        
        var slot = obj.GetComponent<PcbItemSlot>();
        slot.Init();
        slot.Rect.anchorMin = new Vector2(0.5f, 0.5f);
        slot.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        slot.Rect.pivot = new Vector2(0.5f, 0.5f);
        slot.SetData(data);
        slot.SetBlocksRaycasts(false);
        MovePcbItemSlotToScreenPoint(slot, screenPosition, eventCamera);
        UpdatePcbItemSlotDragColor(slot);
        return slot;
    }

    public void RemovePattentSlot(PattentSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        PattentSlotList.Remove(slot);
        slot.Release();
        AssetsManager.Instance.FreeGameObject(slot.gameObject);
        RefreshCompleteButtonState();
    }

    public void DeletePcbItemSlot(PcbItemSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        if (SelectedPcbItemSlot == slot)
        {
            SelectedPcbItemSlot = null;
        }
        SpawnPattentSlot(slot.Data);
        FreePcbItemSlot(slot);
        RefreshCompleteButtonState();
    }

    public void SelectPcbItemSlot(PcbItemSlot slot)
    {
        if (SelectedPcbItemSlot != null && SelectedPcbItemSlot != slot)
        {
            SelectedPcbItemSlot.SetSelected(false);
        }

        SelectedPcbItemSlot = slot;
        SelectedPcbItemSlot.SetSelected(true);
    }

    public void DeselectPcbItemSlot(PcbItemSlot slot)
    {
        if (SelectedPcbItemSlot == slot)
        {
            SelectedPcbItemSlot = null;
        }

        if (slot != null)
        {
            slot.SetSelected(false);
        }
    }

    private PattentSlot SpawnPattentSlot(PcbSlotData data)
    {
        var obj = AssetsManager.Instance.Instantiate(AssetKeys.PattentSlotPath);
        obj.transform.SetParent(memuSlotGroup, false);
        obj.transform.localScale = Vector3.one;

        var slot = obj.transform.GetComponent<PattentSlot>();
        slot.Init();
        slot.SetData(data);
        PattentSlotList.Add(slot);
        RefreshCompleteButtonState();
        return slot;
    }

    public void MovePcbItemSlotToScreenPoint(PcbItemSlot slot, Vector2 screenPosition, Camera eventCamera)
    {
        if (slot == null || pCB == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(pCB, screenPosition, eventCamera, out var localPosition))
        {
            slot.Rect.anchoredPosition = localPosition;
        }
    }

    public bool TryKeepPcbItemSlot(PcbItemSlot slot)
    {
        if (slot == null)
        {
            return false;
        }

        if (!CanKeepPcbItemSlot(slot))
        {
            FreePcbItemSlot(slot);
            return false;
        }

        if (!PcbItemSlotList.Contains(slot))
        {
            PcbItemSlotList.Add(slot);
        }
        slot.SetColor(Color.white);
        return true;
    }

    public void UpdatePcbItemSlotDragColor(PcbItemSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        slot.SetColor(CanKeepPcbItemSlot(slot) ? ValidDragColor : InvalidDragColor);
    }

    public bool CanPlacePcbItemSlot(PcbItemSlot slot)
    {
        return slot != null && CanKeepPcbItemSlot(slot);
    }

    private bool CanKeepPcbItemSlot(PcbItemSlot slot)
    {
        return IsPcbItemSlotInsidePcb(slot) && !IsPcbItemSlotOverlappingOther(slot);
    }

    private bool IsPcbItemSlotOverlappingOther(PcbItemSlot currentSlot)
    {
        if (pCB == null)
        {
            return false;
        }

        var currentPolygons = currentSlot.GetShapePolygonsRelativeTo(pCB);
        var slots = pCB.GetComponentsInChildren<PcbItemSlot>();
        foreach (var slot in slots)
        {
            if (slot == null || slot == currentSlot)
            {
                continue;
            }

            if (PolygonsOverlap(currentPolygons, slot.GetShapePolygonsRelativeTo(pCB)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPcbItemSlotInsidePcb(PcbItemSlot slot)
    {
        var pcbRect = pCB.rect;
        var polygons = slot.GetShapePolygonsRelativeTo(pCB);
        foreach (var polygon in polygons)
        {
            foreach (var point in polygon)
            {
                if (!pcbRect.Contains(point))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool PolygonsOverlap(List<Vector2[]> polygonsA, List<Vector2[]> polygonsB)
    {
        foreach (var polygonA in polygonsA)
        {
            foreach (var polygonB in polygonsB)
            {
                if (PolygonOverlap(polygonA, polygonB))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool PolygonOverlap(Vector2[] polygonA, Vector2[] polygonB)
    {
        if (polygonA.Length < 3 || polygonB.Length < 3)
        {
            return false;
        }

        for (var i = 0; i < polygonA.Length; i++)
        {
            var a1 = polygonA[i];
            var a2 = polygonA[(i + 1) % polygonA.Length];
            for (var j = 0; j < polygonB.Length; j++)
            {
                var b1 = polygonB[j];
                var b2 = polygonB[(j + 1) % polygonB.Length];
                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return IsPointInPolygon(polygonA[0], polygonB) || IsPointInPolygon(polygonB[0], polygonA);
    }

    private bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        var d1 = Cross(a2 - a1, b1 - a1);
        var d2 = Cross(a2 - a1, b2 - a1);
        var d3 = Cross(b2 - b1, a1 - b1);
        var d4 = Cross(b2 - b1, a2 - b1);

        return d1 * d2 < 0f && d3 * d4 < 0f;
    }

    private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            if ((pi.y > point.y) != (pj.y > point.y)
                && point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private void ClearPcbItemSlots()
    {
        for (var i = PcbItemSlotList.Count - 1; i >= 0; i--)
        {
            FreePcbItemSlot(PcbItemSlotList[i]);
        }
        PcbItemSlotList.Clear();
    }

    private void FreePcbItemSlot(PcbItemSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        PcbItemSlotList.Remove(slot);
        if (SelectedPcbItemSlot == slot)
        {
            SelectedPcbItemSlot = null;
        }
        slot.Release();
        AssetsManager.Instance.FreeGameObject(slot.gameObject);
    }


    private void Complete()
    {
        Close();
        CharacterManager.Instance.UlockAccessories();
        UIUtility.PopCompleteWindow();
    }

    private void RefreshCompleteButtonState()
    {
        if (completeBtn == null)
        {
            return;
        }

        completeBtn.interactable = PattentSlotList.Count <= 0;
    }
}
