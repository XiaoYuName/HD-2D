using UnityEngine;
using XFramework;

public partial class SweingMachinePanel : UIBase
{
    private SewingMachineSlot[] sewingMachineSlots;
    private SewingMachineSlotParent[] sewingMachineSlotParents;
    private bool hasLoggedAllSnapped;
    private CanvasGroup mouseCanvasGroup;
    private bool canIronScratch;
    private SewingMachineSlot activeScratchSlot;

    public override void Init()
    {
        InitAutoBind();
        mouseCanvasGroup = mouseParent.GetComponent<CanvasGroup>();
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        InitPreplacedItems();
        
    }

    public void SetData()
    {
        InitPreplacedItems();
    }

    public override void Release()
    {
        StopActiveScratch();

        if (sewingMachineSlots != null)
        {
            for (int i = 0; i < sewingMachineSlots.Length; i++)
            {
                sewingMachineSlots[i]?.Release();
            }
        }

        if (sewingMachineSlotParents != null)
        {
            for (int i = 0; i < sewingMachineSlotParents.Length; i++)
            {
                sewingMachineSlotParents[i]?.Release();
            }
        }

        sewingMachineSlots = null;
        sewingMachineSlotParents = null;
        activeScratchSlot = null;
        base.Release();
    }

    private void InitPreplacedItems()
    {
        hasLoggedAllSnapped = false;
        canIronScratch = false;
        activeScratchSlot = null;
        SewingMachineSlotParent[] slotParents = mouseParent.GetComponentsInChildren<SewingMachineSlotParent>(true);
        sewingMachineSlotParents = slotParents;
        for (int i = 0; i < slotParents.Length; i++)
        {
            slotParents[i].Init();
        }

        SewingMachineSlot[] slots = slotParent.GetComponentsInChildren<SewingMachineSlot>(true);
        sewingMachineSlots = slots;
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].Init();
            slots[i].SetSnapParents(slotParents);
            slots[i].SetSnappedCallback(OnSlotSnapped);
        }
    }

    private void OnSlotSnapped(SewingMachineSlot slot)
    {
        if (!hasLoggedAllSnapped && IsAllSlotsSnapped())
        {
            hasLoggedAllSnapped = true;
            mouseCanvasGroup.alpha = 1f;
            StopActiveScratch();
            canIronScratch = true;
            Debug.Log("缝纫机玩法：所有布料已吸附完毕，可以拖动熨斗。");
        }
    }

    private bool IsAllSlotsSnapped()
    {
        if (sewingMachineSlots == null || sewingMachineSlots.Length <= 0)
        {
            return false;
        }

        for (int i = 0; i < sewingMachineSlots.Length; i++)
        {
            if (sewingMachineSlots[i] == null || !sewingMachineSlots[i].IsSnapped)
            {
                return false;
            }
        }

        return true;
    }

    public bool IsIronScratchReady => canIronScratch;

    public void UpdateIronScratch(Vector2 screenPosition, Camera eventCamera)
    {
        if (!canIronScratch)
        {
            StopActiveScratch();
            return;
        }

        SewingMachineSlot targetSlot = FindSlotAtScreenPosition(screenPosition, eventCamera);
        if (targetSlot == activeScratchSlot)
        {
            return;
        }

        StopActiveScratch();
        activeScratchSlot = targetSlot;
        if (activeScratchSlot != null && activeScratchSlot.StartScratch(GetScratchCamera()))
        {
            Debug.Log($"缝纫机玩法：开启 {activeScratchSlot.name} 的刮刮乐。");
        }
    }

    public void StopIronScratch()
    {
        StopActiveScratch();
    }

    private SewingMachineSlot FindSlotAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        if (sewingMachineSlots == null)
        {
            return null;
        }

        for (int i = sewingMachineSlots.Length - 1; i >= 0; i--)
        {
            SewingMachineSlot slot = sewingMachineSlots[i];
            if (slot == null || !slot.IsSnapped)
            {
                continue;
            }

            RectTransform slotRect = slot.transform as RectTransform;
            if (slotRect != null && RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPosition, eventCamera))
            {
                return slot;
            }
        }

        return null;
    }

    private void StopActiveScratch()
    {
        if (activeScratchSlot == null)
        {
            return;
        }

        activeScratchSlot.SnappedParent?.StopScratch();
        activeScratchSlot = null;
    }

    private Camera GetScratchCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }
}
