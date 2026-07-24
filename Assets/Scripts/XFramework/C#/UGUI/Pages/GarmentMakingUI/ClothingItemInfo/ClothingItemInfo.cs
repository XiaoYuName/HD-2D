using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class ClothingItemInfo : UIBase
{
    private ClothingBag selectedClothingBag = null;
    private int selectedIndex;
    private List<ItemUnlockSlot> selectedItemUnlockSlots = new List<ItemUnlockSlot>();
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(indexUpButton,OnUpShowData,"");
        Bind(indexDownButton,OnDownShowData,"");
        //Bind();
    }
    
    public void SetDataList(ClothingBag clothingList,ClothingAccessoriesData accessoriesData)
    {
        selectedClothingBag = clothingList;
        selectedIndex = 0;
        for (int i = 0; i < LubanManager.Instance.TbClothingAccessoriesData.DataList.Count; i++)
        {
            var data = LubanManager.Instance.TbClothingAccessoriesData.DataList[i];
            if (data.ID == accessoriesData.ID)
            {
                selectedIndex = i;
            }
        }
        ShowData(selectedClothingBag.Accessories[selectedIndex]);
        
    }
    
    public void ShowData(ClothingAccessoriesBag clothingAccessoriesBag)
    {
        indexTex.text = $"{selectedIndex + 1}";
        foreach (var Slot in selectedItemUnlockSlots)
        {
            Slot.Close();
            AssetsManager.Instance.FreeGameObject(Slot.gameObject);
        }
        selectedItemUnlockSlots.Clear();

        ClothingAccessoriesData clothingAccessoriesData =
            LubanManager.Instance.TbClothingAccessoriesData.Get(clothingAccessoriesBag.accessoriesID);
        
        foreach (var da in clothingAccessoriesData.Consumption)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.ItemUnlockSlotPath);
            obj.transform.SetParent(itemScroll.content);
            obj.transform.localScale  = Vector3.one;
            var slot  = obj.GetComponent<ItemUnlockSlot>();
            slot.Init();
            slot.SetData(da);
            selectedItemUnlockSlots.Add(slot);
        }
    }
    
    public void OnUpShowData()
    {
        selectedIndex -= 1;
        if (selectedIndex < 0)
        {
            selectedIndex = selectedClothingBag.Accessories.Count - 1;
        }

        ShowData(selectedClothingBag.Accessories[selectedIndex]);
    }

    public void OnDownShowData()
    {
        selectedIndex += 1;
        if (selectedIndex > selectedClothingBag.Accessories.Count - 1)
        {
            selectedIndex = 0;
        }
        ShowData(selectedClothingBag.Accessories[selectedIndex]);
    }
}
