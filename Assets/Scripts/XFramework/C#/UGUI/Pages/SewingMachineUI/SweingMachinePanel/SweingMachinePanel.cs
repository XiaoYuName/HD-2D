using UnityEngine;
using XFramework;

public partial class SweingMachinePanel : UIBase
{
    private SewingMachineSlot[] sewingMachineSlots;
    private SewingMachineSlotParent[] sewingMachineSlotParents;
    private bool hasLoggedAllSnapped;
    private CanvasGroup mouseCanvasGroup;

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
        base.Release();
    }

    private void InitPreplacedItems()
    {
        hasLoggedAllSnapped = false;
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
            //StartScratchImages();
            //Debug.Log("缝纫机玩法：所有布料已吸附完毕。");
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

    private void StartScratchImages()
    {
        Camera scratchCamera = GetScratchCamera();
        int startedCount = 0;
        for (int i = 0; i < sewingMachineSlots.Length; i++)
        {
            if (sewingMachineSlots[i] != null && sewingMachineSlots[i].StartScratch(scratchCamera))
            {
                startedCount++;
            }
        }

        Debug.Log($"缝纫机玩法：刮刮乐已启动 {startedCount}/{sewingMachineSlots.Length}。");
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
