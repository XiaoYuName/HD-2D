using UnityEngine;
using XFramework;

public partial class ItemUnlockSlot : UIBase
{
    public ItemData currentItemData { get; private set; }
    public TbConsumption consumablesData { get; private set; }
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public override void Release()
    {
        base.Release();
        currentItemData = null;
        consumablesData = null;
    }

    public void SetData(TbConsumption consumption)
    {
        currentItemData = InventoryManager.Instance.GetItemData(consumption.ItemID);
        consumablesData = consumption;
        
        itemCount.text = $"{InventoryManager.Instance.GetItemCount(consumption.ItemID)}";
        itemIcon.sprite = LoadAsset<Sprite>(GamePathTools.CombinationItemIconPath(currentItemData.IconName));
        itemNameString.SetText(currentItemData.NameKey);
        itemCountTex.text = $"X{consumablesData.Count}";
    }

    public void Refresh()
    {
        SetData(consumablesData);
    }
}
