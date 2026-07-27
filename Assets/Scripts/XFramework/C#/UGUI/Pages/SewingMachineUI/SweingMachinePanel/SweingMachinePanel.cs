using XFramework;

public partial class SweingMachinePanel : UIBase
{
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

    private void InitPreplacedItems()
    {
        SewingMachineSlotParent[] slotParents = mouseParent.GetComponentsInChildren<SewingMachineSlotParent>(true);
        for (int i = 0; i < slotParents.Length; i++)
        {
            slotParents[i].Init();
        }

        SewingMachineSlot[] slots = slotParent.GetComponentsInChildren<SewingMachineSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].Init();
        }
    }
}
