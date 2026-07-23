using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class AccessoriesSlot : UIBase
{
    public ClothingAccessoriesData AccessoriesData { get; private set; } 
    private List<ItemUnlockSlot> ItemUnlockSlots = new List<ItemUnlockSlot>();
    public ClothingBag ClothingBag { get; private set; }

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        InventoryManager.Instance.RegisterAllItemChange(UpdateCheck);
    }

    public override void Close()
    {
        base.Close();
        InventoryManager.Instance.UnregisterAllItemChange(UpdateCheck);
    }

    public override void Release()
    {
        base.Release();
        foreach (var da in ItemUnlockSlots)
        {
            da.Release();
            AssetsManager.Instance.FreeGameObject(da.gameObject);
        }
        ItemUnlockSlots.Clear();
    }

    public void SetData(ClothingBag clothingBag,ClothingAccessoriesData  data)
    {
        AccessoriesData = data;
        ClothingBag = clothingBag;
        nameKey.SetText(data.AccessoriesName);
        icon.sprite = LoadAsset<Sprite>(GamePathTools.CombinationAccessoriesIconPath(data.AccessoriesIconName));
        
        foreach (var da in ItemUnlockSlots)
        {
            da.Release();
            AssetsManager.Instance.FreeGameObject(da.gameObject);
        }
        ItemUnlockSlots.Clear();
        foreach (var da in data.Consumption)
        {
           var obj = AssetsManager.Instance.Instantiate(AssetKeys.ItemUnlockSlotPath);
           obj.transform.SetParent(itemScroll.content);
           obj.transform.localScale  = Vector3.one;
           var slot  = obj.GetComponent<ItemUnlockSlot>();
           slot.Init();
           slot.SetData(da);
           ItemUnlockSlots.Add(slot);
        }
    }

    private void UpdateCheck(List<ItemInfo> itemInfos)
    {
        if (ClothingBag.isUnlock)
        {
            button.interactable = false;
            button.SetLabel("UIText","Complete");
        }
        else
        {
            bool isUnlock = CheckUnlock(AccessoriesData);
            if (isUnlock)
            {
                button.interactable = true;
                button.SetLabel("GarmentMakingUI","GameLabelButton");
            }
            else
            {
                button.interactable = false;
                button.SetLabel("GarmentMakingUI","InsufficientMaterials");
            }
        }

        foreach (var slot in ItemUnlockSlots)
        {
            slot.Refresh();
        }
        
        
    }

    private bool CheckUnlock(ClothingAccessoriesData slot)
    {
        foreach (var tbConsumption in slot.Consumption)
        {
            if (InventoryManager.Instance.GetItemCount(tbConsumption.ItemID) < tbConsumption.Count)
            {
                return false;
            }
        }
        return true;
        
    }
}
