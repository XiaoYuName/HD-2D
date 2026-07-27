using UnityEngine;
using XFramework;

public partial class SweingMachinePanel : UIBase
{
    private SewingMachineSlot[] sewingMachineSlots;
    private bool hasLoggedAllSnapped;

    public override void Init()
    {
        InitAutoBind();

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

        sewingMachineSlots = null;
        base.Release();
    }

    private void InitPreplacedItems()
    {
        hasLoggedAllSnapped = false;
        SewingMachineSlotParent[] slotParents = mouseParent.GetComponentsInChildren<SewingMachineSlotParent>(true);
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
            Debug.Log("缝纫机玩法：所有布料已吸附完毕。");
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
}
