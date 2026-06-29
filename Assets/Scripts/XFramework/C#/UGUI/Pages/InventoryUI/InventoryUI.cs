using System;
using System.Collections.Generic;
using System.Linq;
using Coffee.UIEffects;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class InventoryUI : UIBase
{
    private ScrollRect itemTypeButtonScrollRect;
    private ScrollRect itemScrollRect;
    private CustomButton closeButton;

    private List<ItemBagSlot> itemBagList = new List<ItemBagSlot>();
    private LabelButton AllItemTypeButton;
    private Dictionary<ItemType, LabelButton> itemTypeButtonList;
    private LocalSelectedData _localSelectedData;
    private ItemInfoUI _itemInfoUI;
    private CustomDropdownUI  _dropdownUI;
    private Button reverseButton;
    private UIEffect reverseUIEffect;
    
    public bool isReverseOrder;
    private ItemSortType _itemSortType;
    
    private LocalizeStringEvent stringEvent;

    private List<ItemBag> CurrentBagList;

    
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        
        CurrentBagList = new List<ItemBag>();
        stringEvent = Get<LocalizeStringEvent>("UIMask/Page/Top/CurrentGoldFarme/GoldText");
        itemTypeButtonScrollRect = Get<ScrollRect>("UIMask/Page/Left/ButtonContent/Scroll View");
        itemScrollRect = Get<ScrollRect>("UIMask/Page/Scroll View");
        closeButton = Get<CustomButton>("UIMask/Page/Top/CloseButton");
        reverseButton = Get<Button>("UIMask/Page/Left/ReverseButton");
        reverseUIEffect = Get<UIEffect>("UIMask/Page/Left/ReverseButton");
        
        Bind(reverseButton, () =>
        {
            SetReverseOrder(!isReverseOrder);
            if (isReverseOrder)
            {
                reverseUIEffect.flip = Flip.Vertical;
            }
            else
            {
                reverseUIEffect.flip = Flip.Effect;
            }


        },"");
        itemTypeButtonList = new Dictionary<ItemType, LabelButton>();
        _itemInfoUI = Get<ItemInfoUI>("UIMask/Page/ItemInfoUI");
        _itemInfoUI.Init();
        _dropdownUI = Get<CustomDropdownUI>("UIMask/Page/Left/CustomDropdown");
        _dropdownUI.Init();
        _dropdownUI.SetItemSortType(OptionSortType);
      
        Bind(closeButton, Close,"");
        CreatItemType();
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        InventoryManager.Instance.RegisterAllItemChange(UpdateItemBags);
        GameDataManager.Instance.BindPlayerDataChange(UpdatePlayerDataChange);
        OptionType(_localSelectedData);
        PlayerInputManager.Instance.OnRightClick += Close;
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnBindPlayerDataChange(UpdatePlayerDataChange);
        InventoryManager.Instance.UnregisterAllItemChange(UpdateItemBags);
        PlayerInputManager.Instance.OnRightClick -= Close;
    }

    private void OnDestroy()
    {
        if (itemTypeButtonList != null)
        {
            foreach (var key in itemTypeButtonList.Keys)
            {
                AssetsManager.Instance.FreeGameObject(itemTypeButtonList[key].gameObject);
            }
            itemTypeButtonList.Clear();
        }
        AssetsManager.Instance.FreeGameObject(AllItemTypeButton.gameObject);
        AllItemTypeButton = null;
    }

    private void CreatItemType()
    {
        var allObj = AssetsManager.Instance.Instantiate(AssetKeys.InventoryLableButtonPath);
        allObj.transform.SetParent(itemTypeButtonScrollRect.content);
        allObj.transform.localScale = Vector3.one;
        var allBtn = allObj.GetComponent<LabelButton>();
        allBtn.Init();
        allBtn.SetData(new LocalSelectedData()
        {
            Table = "EnumsText",
            Value =  "All",
        },OptionType);
        AllItemTypeButton = allBtn;
        foreach (ItemType itemType in Enum.GetValues(typeof(ItemType)))
        {
            
            if(itemType == ItemType.None)continue;
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.InventoryLableButtonPath);
            obj.transform.SetParent(itemTypeButtonScrollRect.content);
            obj.transform.localScale = Vector3.one;
            var LabelBtn = obj.GetComponent<LabelButton>();
            LabelBtn.Init();
            LabelBtn.SetData(new LocalSelectedData()
            {
                Table = "EnumsText",
                Value =  itemType.ToString(),
            },OptionType);
            itemTypeButtonList.Add(itemType, LabelBtn);
        }

        _localSelectedData = allBtn.SelectedData;
    }

    private void UpdatePlayerDataChange(PlayerData user)
    {
        stringEvent.StringReference.SetVar("value",user.GetProperty(PropertyType.Gold));
    }

    private void UpdateItemBags(List<ItemBag> bags)
    {
        if (bags.Count <= 0)
        {
            foreach (ItemBagSlot bagSlot in itemBagList)
            {
                bagSlot.Release();
                AssetsManager.Instance.FreeGameObject(bagSlot.gameObject);
            }
            itemBagList.Clear();
            CurrentBagList.Clear();
            OptionItemBag(null);
            return;
        }

        CurrentBagList =  ApplySort(bags);
        if (itemBagList.Count <= 0)
        {
            for (int i = 0; i < CurrentBagList.Count; i++)
            {
                var obj = AssetsManager.Instance.Instantiate(AssetKeys.ItemBagSlotPath);
                obj.transform.SetParent(itemScrollRect.content);
                obj.transform.localScale = Vector3.one;
                ItemBagSlot bagSlot = obj.GetComponent<ItemBagSlot>();
                bagSlot.Init();
                bagSlot.SetData(CurrentBagList[i],OptionItemBag);
               
                itemBagList.Add(bagSlot);
            }
        }
        else
        {
            for (int i = 0; i < CurrentBagList.Count; i++)
            {
                if (i <= itemBagList.Count - 1)
                {
                    itemBagList[i].Release();
                    itemBagList[i].SetData(CurrentBagList[i],OptionItemBag);
                }
                else
                {
                    var obj = AssetsManager.Instance.Instantiate(AssetKeys.ItemBagSlotPath);
                    obj.transform.SetParent(itemScrollRect.content);
                    obj.transform.localScale = Vector3.one;
                    ItemBagSlot bagSlot = obj.GetComponent<ItemBagSlot>();
                    bagSlot.Init();
                    bagSlot.SetData(CurrentBagList[i],OptionItemBag);
                    itemBagList.Add(bagSlot);
                }
            }
            int index = itemBagList.Count - 1;
            while (index > CurrentBagList.Count - 1)
            {
                itemBagList[index].Release();
                AssetsManager.Instance.FreeGameObject(itemBagList[index].gameObject);
                itemBagList.RemoveAt(index);
                index--;
            }
        }
    }

    private void OptionType(LocalSelectedData selectedType)
    {
        this._localSelectedData = selectedType;
        if (selectedType.Value == "All")
        {
            AllItemTypeButton.SetSelected(true);
            foreach (var key in itemTypeButtonList.Keys)
            {
                itemTypeButtonList[key].SetSelected(false);
            }
            foreach (var slot in itemBagList)
            {
                slot.gameObject.SetActive(true);
            }
            return;
        }

        AllItemTypeButton.SetSelected(false);
        foreach (var key in itemTypeButtonList.Keys)
        {
            if (key.ToString() == selectedType.Value)
            {
                itemTypeButtonList[key].SetSelected(true);
            }
            else
            {
                itemTypeButtonList[key].SetSelected(false);
            }
        }

        ItemType OptionType = ItemType.None;
        foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
        {
            if (type.ToString() == selectedType.Value)
            {
                OptionType = type;
            }
        }
        
        foreach (var slot in itemBagList)
        {
            slot.gameObject.SetActive(slot.itemData.Type == OptionType);
        }


    }

    private void OptionItemBag(ItemBagSlot bagSlot)
    {
        if (bagSlot == null)
        {
            for (int i = 0; i < itemBagList.Count; i++)
            {
                itemBagList[i].SetSelected(false);
            }
            _itemInfoUI.SetData(null);
        }
        else
        {
            for (int i = 0; i < itemBagList.Count; i++)
            {
                if (itemBagList[i] == bagSlot)
                {
                    itemBagList[i].SetSelected(true);
                }
                else
                {
                    itemBagList[i].SetSelected(false);
                }
            }
            _itemInfoUI.SetData(bagSlot.itemBag);
        }

        
    }
    

    private void OptionSortType(ItemSortType sortType)
    {
        _itemSortType = sortType;
        UpdateItemBags(CurrentBagList);
    }

    [Button("设置反转")]
    public void SetReverseOrder(bool reverse)
    {
        isReverseOrder = reverse;
        UpdateItemBags(CurrentBagList);
    }

    private List<ItemBag> ApplySort(List<ItemBag> itemBags)
    {
        switch (_itemSortType)
        {
            case ItemSortType.CreatTime:
                return !isReverseOrder ? itemBags.OrderByDescending(x => x.CreateTime).ToList() : itemBags.OrderBy(x => x.CreateTime).ToList();
            case ItemSortType.Number:
                return !isReverseOrder ? itemBags.OrderByDescending(x => x.itemAmount).ToList() : itemBags.OrderBy(x => x.itemAmount).ToList();
            case ItemSortType.Quality:
                return !isReverseOrder ? itemBags.OrderByDescending(x => InventoryManager.Instance.GetItemData(x.itemID).Quality).ToList() : 
                    itemBags.OrderBy(x =>InventoryManager.Instance.GetItemData(x.itemID).Quality).ToList();
        }
        return itemBags;
    }

}

