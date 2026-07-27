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
    private float scratchCompleteRatio = 0.8f;
    private bool hasLoggedAllScratchCompleted;

    public override void Init()
    {
        InitAutoBind();
        mouseCanvasGroup = mouseParent.GetComponent<CanvasGroup>();
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        InitPreplacedItems();
        
    }

    public void SetData(SewingMachineGameData setting)
    {
        scratchCompleteRatio = setting == null ? 0.8f : Mathf.Clamp01(setting.ScratchCompleteRatio);
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
        hasLoggedAllScratchCompleted = false;
        canIronScratch = false;
        activeScratchSlot = null;
        // 面板是对象池复用的，alpha 不重置的话第二次打开一进来布料就是显示的
        if (mouseCanvasGroup != null)
        {
            mouseCanvasGroup.alpha = 0f;
        }

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

        WarnMismatchedParents();
    }

    /// <summary>
    /// 每个 SlotParent 都要有一块类型对应的布，否则它永远刮不到，
    /// 整关的"全部完成"就永远不会触发。配错了直接报出来，别让玩法静默卡死。
    /// </summary>
    private void WarnMismatchedParents()
    {
        for (int i = 0; i < sewingMachineSlotParents.Length; i++)
        {
            SewingMachineSlotParent target = sewingMachineSlotParents[i];
            if (target == null)
            {
                continue;
            }

            bool hasSlot = false;
            for (int j = 0; j < sewingMachineSlots.Length; j++)
            {
                if (sewingMachineSlots[j] != null && sewingMachineSlots[j].ParentType == target.ParentType)
                {
                    hasSlot = true;
                    break;
                }
            }

            if (!hasSlot)
            {
                Debug.LogWarning($"缝纫机玩法：{target.name}（{target.ParentType}）没有对应类型的布料，该关卡无法完成。", target);
            }
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

        // StopActiveScratch 会给上一块布做最后一次结算，有可能当场把整关判完
        StopActiveScratch();

        if (!canIronScratch || targetSlot == null || targetSlot.IsScratchCompleted)
        {
            CheckAllScratchCompleted();
            return;
        }

        activeScratchSlot = targetSlot;
        if (!activeScratchSlot.StartScratch(GetScratchCamera(), scratchCompleteRatio, OnScratchCompleted))
        {
            // 这块布已经刮完（或者没法刮），不要把它挂成当前目标，
            // 否则熨斗会一直"卡"在它身上，移到别的布上也不再触发。
            activeScratchSlot = null;
            CheckAllScratchCompleted();
        }
    }

    public void StopIronScratch()
    {
        StopActiveScratch();
        CheckAllScratchCompleted();
    }

    private SewingMachineSlot FindSlotAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        if (sewingMachineSlots == null)
        {
            return null;
        }

        // 布料之间的 Rect 有重叠，已经刮完的要跳过，
        // 否则压在下面那块没刮完的布永远选不中。
        for (int i = sewingMachineSlots.Length - 1; i >= 0; i--)
        {
            SewingMachineSlot slot = sewingMachineSlots[i];
            if (slot == null || !slot.IsSnapped || slot.IsScratchCompleted)
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

    private void OnScratchCompleted(SewingMachineSlotParent completedParent)
    {
        if (activeScratchSlot != null && activeScratchSlot.SnappedParent == completedParent)
        {
            activeScratchSlot = null;
        }

        CheckAllScratchCompleted();
    }

    private void CheckAllScratchCompleted()
    {
        if (hasLoggedAllScratchCompleted || !IsAllScratchCompleted())
        {
            return;
        }

        hasLoggedAllScratchCompleted = true;
        canIronScratch = false;
        StopActiveScratch();
        Debug.Log("缝纫机玩法：所有布料刮刮乐已完成。");
        var ui = UISystem.Instance.GetUI<SewingMachineUI>("SewingMachineUI");
        if (ui != null)
        {
            ui.Complete();
        }
    }

    private bool IsAllScratchCompleted()
    {
        if (sewingMachineSlotParents == null || sewingMachineSlotParents.Length <= 0)
        {
            return false;
        }

        for (int i = 0; i < sewingMachineSlotParents.Length; i++)
        {
            if (sewingMachineSlotParents[i] == null || !sewingMachineSlotParents[i].IsScratchCompleted)
            {
                return false;
            }
        }

        return true;
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
