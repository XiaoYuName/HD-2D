using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 布料商店
/// </summary>
public class ClothShopUI : UIBase
{
    private LocalizeStringEvent currentGoldStringEvent;
    private LocalizeStringEvent allPriceStringEvent;
    private Button closeButton;
    private Button BuyAllButton;

    private ScrollRect shopItemTypeScrollRect;
    private ScrollRect shopItemScrollRect;
    private ScrollRect shopBuyItemScrollRect;
    
    
    private List<ClothShopItemSlot>  ShopItemBags = new List<ClothShopItemSlot>();
    private LabelButton AllItemTypeButton;
    private Dictionary<ItemType, LabelButton> itemTypeButtonList;
    private List<ClothBuyItemSlot> buyItemSlotList = new List<ClothBuyItemSlot>();
    private LocalSelectedData _localSelectedData;
    
    private List<ShopItemBag> _shopItems = new List<ShopItemBag>();
    
    private ShopMode  _shopMode;
    private RectTransform BuyRect;
    private RectTransform SellRect;
    private Button OptionSellButton;
    private Button OptionBuyButton;
    
    
    private List<ItemBag> CurrentBagList = new List<ItemBag>();
    private List<ItemBagSlot> itemBagList = new List<ItemBagSlot>();
    private ScrollRect itemScrollRect;


    [FoldoutGroup("提示多语言"),LabelText("标题")]
    public LocalSelectedData commTip;
    [FoldoutGroup("提示多语言"),LabelText("成功文案")]
    public LocalSelectedData successContent;
    [FoldoutGroup("提示多语言"),LabelText("失败文案")]
    public LocalSelectedData errorContent;
    [FoldoutGroup("提示多语言"),LabelText("按钮多语言")]
    public LocalSelectedData okButton;

    [FoldoutGroup("选项按钮"),HorizontalGroup("选项按钮/ButtonGroups"),LabelText("选中颜色")]
    public Color SelectedBtnColor;
    
    [FoldoutGroup("选项按钮"),HorizontalGroup("选项按钮/ButtonGroups"),LabelText("未选中颜色")]
    public Color UnSelectedBtnColor;
    
    [FoldoutGroup("选项按钮"),HorizontalGroup("选项按钮/动态Type"),LabelText("ItemColors")]
    public Color[] ItemTypeBtnColor;
    
    
    
    private LocalizeStringEvent selectedItemNameStringEvent;
    private LocalizeStringEvent selectedItemDescStringEvent;
    private LocalizeStringEvent selectedSellItemPriceStringEvent;
    private RectTransform NullMask;
    private ItemBagSlot selectedItemSlot;
    private CustomDropdownUI _customDropdownUI;
    private int selectedItemSlotNumber;
    private ItemSortType _itemSortType;
    private Button SellButton;
    
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        BuyRect = Get<RectTransform>("UIMask/Panel/ShopRect");
        SellRect = Get<RectTransform>("UIMask/Panel/SellRect");
        currentGoldStringEvent = Get<LocalizeStringEvent>("UIMask/Panel/CurrentGoldValue/ValueTex");
        allPriceStringEvent = Get<LocalizeStringEvent>("UIMask/Panel/ShopRect/Panel/AllPriceTex");
        
        selectedItemNameStringEvent = Get<LocalizeStringEvent>("UIMask/Panel/SellRect/Panel/SelectedItemNameString");
        selectedItemDescStringEvent = Get<LocalizeStringEvent>("UIMask/Panel/SellRect/Panel/SelectedItemDesString");
        selectedSellItemPriceStringEvent = Get<LocalizeStringEvent>("UIMask/Panel/SellRect/ItemFarme/SellPriceTex");
        NullMask = Get<RectTransform>("UIMask/Panel/SellRect/Panel/NullMask");
        
        closeButton = Get<Button>("UIMask/Panel/ShopRect/CloseButton");
        BuyAllButton = Get<Button>("UIMask/Panel/ShopRect/Panel/BuyAllButton");
        shopItemTypeScrollRect = Get<ScrollRect>("UIMask/Panel/ButtonGroup/Scroll View");
        shopItemScrollRect = Get<ScrollRect>("UIMask/Panel/ShopRect/ItemFarme/ShopFarme/Scroll View");
        shopBuyItemScrollRect = Get<ScrollRect>("UIMask/Panel/ShopRect/Panel/ItemFarme/BuyScrollRect");

        OptionSellButton = Get<Button>("UIMask/Panel/OptionSellButton");
        OptionBuyButton = Get<Button>("UIMask/Panel/OptionBuyButton");
        SellButton = Get<Button>("UIMask/Panel/SellRect/ItemFarme/SellButton");

        itemScrollRect = Get<ScrollRect>("UIMask/Panel/SellRect/ItemFarme/ShopFarme/Scroll View");
        _customDropdownUI = Get<CustomDropdownUI>("UIMask/Panel/SellRect/ItemFarme/ShopFarme/Image/CustomDropdown");
        _customDropdownUI.Init();
        _customDropdownUI.SetItemSortType(OptionSortType);
        
        GenerateItemTypeButtons();
        Bind(closeButton,Close,"");
        Bind(BuyAllButton,SettlementShop,"");
        Bind(OptionBuyButton, () =>
        {
            OptionShowMode(ShopMode.Buy);
        },"");
        Bind(OptionSellButton, () =>
        {
            OptionShowMode(ShopMode.Sell);
        },"");
        Bind(SellButton,SellItem,"");
        CalculateTotalPrice();
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.BindPlayerDataChange(UpdatePlayerDataUI);
        ShopManager.Instance.BindClothShopChange(GenerateShopItems);
        InventoryManager.Instance.RegisterAllItemChange(GenerateInventoryItem);
        OptionType(_localSelectedData);
        OptionShowMode(ShopMode.Buy);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnBindPlayerDataChange(UpdatePlayerDataUI);
        ShopManager.Instance.UnBindClothShopChange(GenerateShopItems);
        InventoryManager.Instance.UnregisterAllItemChange(GenerateInventoryItem);
    }

    private void GenerateItemTypeButtons()
    {
        itemTypeButtonList = new Dictionary<ItemType, LabelButton>();
        var allObj = AssetsManager.Instance.Instantiate(AssetKeys.ClothItemLabelButtonPath);
        allObj.transform.SetParent(shopItemTypeScrollRect.content);
        allObj.transform.localScale = Vector3.one;
        var allBtn = allObj.GetComponent<LabelButton>();
        allBtn.SelectedColor = ItemTypeBtnColor[0];
        allBtn.Init();
        allBtn.SetData(new LocalSelectedData()
        {
            Table = "EnumsText",
            Value =  "All",
        },OptionType);
       
        AllItemTypeButton = allBtn;
        int ColorIndex = 1;
        foreach (ItemType itemType in Enum.GetValues(typeof(ItemType)))
        {
            if(itemType == ItemType.None)continue;
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.ClothItemLabelButtonPath);
            obj.transform.SetParent(shopItemTypeScrollRect.content);
            obj.transform.localScale = Vector3.one;
            LabelButton btn = obj.GetComponent<LabelButton>();
            if (ColorIndex >= ItemTypeBtnColor.Length)
            {
                btn.SelectedColor = ItemTypeBtnColor[0];
            }
            else
            {
                btn.SelectedColor = ItemTypeBtnColor[ColorIndex];
            }

           
            btn.Init();
            btn.SetData(new LocalSelectedData()
            {
                Table = "EnumsText",
                Value =  itemType.ToString(),
            },OptionType);
            itemTypeButtonList.Add(itemType,btn);
        }
        
        _localSelectedData = allBtn.SelectedData;
    }

    private void GenerateShopItems(List<ShopItemBag> shopItems)
    {
        _shopItems = new List<ShopItemBag>();
        for (int i = 0; i < shopItems.Count; i++)
        {
            ShopItemBag data = new ShopItemBag();
            data.ItemID =  shopItems[i].ItemID;
            data.ItemNumber =  shopItems[i].ItemNumber;
            _shopItems.Add(data);
        }
        
        
        
        if (shopItems.Count <= 0)
        {
            foreach (ClothShopItemSlot bagSlot in ShopItemBags)
            {
                bagSlot.Release();
                AssetsManager.Instance.FreeGameObject(bagSlot.gameObject);
            }
            return;
        }
        
        if (ShopItemBags.Count <= 0)
        {
            for (int i = 0; i < shopItems.Count; i++)
            {
                var obj = AssetsManager.Instance.Instantiate(AssetKeys.ClothShopItemSlotPath);
                obj.transform.SetParent(shopItemScrollRect.content);
                obj.transform.localScale = Vector3.one;
                ClothShopItemSlot bagSlot = obj.GetComponent<ClothShopItemSlot>();
                bagSlot.Init();
                bagSlot.SetData(shopItems[i]);
                ShopItemBags.Add(bagSlot);
            }
            return;
        }

        for (int i = 0; i < shopItems.Count; i++)
        {
            if (i <= ShopItemBags.Count - 1)
            {
                ShopItemBags[i].Release();
                ShopItemBags[i].SetData(shopItems[i]);
            }
            else
            {
                var obj = AssetsManager.Instance.Instantiate(AssetKeys.ClothShopItemSlotPath);
                obj.transform.SetParent(shopItemScrollRect.content);
                obj.transform.localScale = Vector3.one;
                ClothShopItemSlot bagSlot = obj.GetComponent<ClothShopItemSlot>();
                bagSlot.Init();
                bagSlot.SetData(shopItems[i]);
                ShopItemBags.Add(bagSlot);
            }
        }
        int index = ShopItemBags.Count - 1;
        while (index > shopItems.Count - 1)
        {
            ShopItemBags[index].Release();
            AssetsManager.Instance.FreeGameObject(ShopItemBags[index].gameObject);
            ShopItemBags.RemoveAt(index);
            index--;
        }
    }


    public void AddBuyItem(ClothShopItemSlot bagSlot)
    {
        AddBuyItem(bagSlot.ShopItemData);
    }

    public void AddBuyItem(ClothBuyItemSlot  bagSlot)
    {
        AddBuyItem(bagSlot.ClothShopData);
    }

    public void AddBuyItem(ClothShopData data)
    {
        int shopIndex = _shopItems.FindIndex(t => t.ItemID == data.ItemID);
        if (shopIndex >= 0)
        {
            if (_shopItems[shopIndex].ItemNumber > 0)
            {
                //扣除商店里的数量
                _shopItems[shopIndex].ItemNumber--;
                if (buyItemSlotList.Any(t => t.ClothShopData.ItemID == data.ItemID))
                {
                    int index = buyItemSlotList.FindIndex(temp => temp.ClothShopData.ItemID == data.ItemID);
                    if (index >= 0)
                    {
                        var clothShopData =  buyItemSlotList[index].ItemBag;
                        clothShopData.ItemNumber++;
                        buyItemSlotList[index].SetData(clothShopData);
                    }
                }
                else
                {
                    var obj = AssetsManager.Instance.Instantiate(AssetKeys.ClothBuyItemSlotPath);
                    obj.transform.SetParent(shopBuyItemScrollRect.content);
                    obj.transform.localScale = Vector3.one;
                    var buy = obj.GetComponent<ClothBuyItemSlot>();
                    buy.Init();
                    ShopItemBag clothShopItemBag =  new ShopItemBag()
                    {
                        ItemID =  data.ItemID,
                        ItemNumber = 1,
                    };
                    buy.SetData(clothShopItemBag);
                    buyItemSlotList.Add(buy);
                }
            
            }
            GenerateShopItems(_shopItems);
            CalculateTotalPrice();
        }
    }



    public void RemoveBuyItem(ClothShopData data)
    {
        int shopIndex = _shopItems.FindIndex(t => t.ItemID == data.ItemID);
        if (shopIndex >= 0)
        {
            _shopItems[shopIndex].ItemNumber++;
            if (buyItemSlotList.Any(t => t.ClothShopData.ItemID == data.ItemID))
            {
                var index = buyItemSlotList.FindIndex(t => t.ClothShopData.ItemID == data.ItemID);
                var clothShopData =  buyItemSlotList[index].ItemBag;
                clothShopData.ItemNumber--;
                buyItemSlotList[index].SetData(clothShopData);
                if (clothShopData.ItemNumber <= 0)
                {
                    AssetsManager.Instance.FreeGameObject(buyItemSlotList[shopIndex].gameObject);
                    buyItemSlotList.RemoveAt(index);
                }
            }
            GenerateShopItems(_shopItems);
            CalculateTotalPrice();
        }
    }

    public void RemoveBuyItem(ClothShopItemSlot bagSlot)
    {
        RemoveBuyItem(bagSlot.ShopItemData);
    }

    public void RemoveBuyItem(ClothBuyItemSlot bagSlot)
    {
        RemoveBuyItem(bagSlot.ClothShopData);
    }



    public void CalculateTotalPrice()
    {
        int price = 0;
        foreach (var bagSlot in buyItemSlotList)
        {
            price += bagSlot.ClothShopData.ItemNumber * bagSlot.ClothShopData.Price ;
        }
        
        allPriceStringEvent.SetVar("value",price);
        if (price <= 0)
        {
            BuyAllButton.interactable = false;
        }
        else
        {
            BuyAllButton.interactable = GameDataManager.Instance.GetProperty(PropertyType.Gold).Value >= price;
        }
    }

    /// <summary>
    /// 结算所有商品
    /// </summary>
    private void SettlementShop()
    {
        var price = buyItemSlotList.Sum(t => t.ClothShopData.ItemNumber * t.ClothShopData.Price);
        if (GameDataManager.Instance.GetProperty(PropertyType.Gold).Value >= price)
        {
            GameDataManager.Instance.RemoveProperty(PropertyType.Gold,price);
            foreach (var bagSlot in buyItemSlotList)
            {
                InventoryManager.Instance.AddItem(bagSlot.ClothShopData.ItemID,bagSlot.ClothShopData.ItemNumber);
                AssetsManager.Instance.FreeGameObject(bagSlot.gameObject);
            }
            buyItemSlotList.Clear();
            ShopManager.Instance.SetClothShops(_shopItems);
            UIUtility.ShowPopWindow(commTip,successContent,okButton);
            SaveGameManager.Instance.Save();
        }
        else
        {
            UIUtility.ShowPopWindow(commTip,errorContent,okButton);
        }


    }
    
    
    private void UpdatePlayerDataUI(PlayerData user)
    {
        currentGoldStringEvent.SetVar("value",GameDataManager.Instance.GetProperty(PropertyType.Gold).Value);
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
            foreach (var slot in ShopItemBags)
            {
                slot.gameObject.SetActive(true);
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
        
        foreach (var slot in ShopItemBags)
        {
            slot.gameObject.SetActive(slot.ItemData.Type == OptionType);
        }

        foreach (var slot in itemBagList)
        {
            slot.gameObject.SetActive(slot.itemData.Type == OptionType);
        }


    }
    
    private void OptionShowMode(ShopMode shopMode)
    {
        _shopMode = shopMode;
        switch (_shopMode)
        {
            case ShopMode.Buy:
                BuyRect.gameObject.SetActive(true);
                SellRect.gameObject.SetActive(false);
                OptionBuyButton.targetGraphic.color = SelectedBtnColor;
                OptionSellButton.targetGraphic.color = UnSelectedBtnColor;
                break;
            case ShopMode.Sell:
                SellRect.gameObject.SetActive(true);
                BuyRect.gameObject.SetActive(false);
                if (selectedItemSlot != null)
                {
                    selectedItemSlot.SetSelected(false);
                    selectedItemSlot.ActiveSelectedNumber(false);
                }
                selectedItemSlot = null;
                selectedItemSlotNumber = 0;
                CalculateTotalSellPrice();
                OptionBuyButton.targetGraphic.color = UnSelectedBtnColor;
                OptionSellButton.targetGraphic.color = SelectedBtnColor;
                break;
        }

        _localSelectedData = AllItemTypeButton.SelectedData;
        OptionType(_localSelectedData);
    }

    private void GenerateInventoryItem(List<ItemBag> bags)
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
            //OptionItemBag(null);
            return;
        }

        //CurrentBagList = bags;
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
                bagSlot.SetData(CurrentBagList[i],SelectedBagItem);
                bagSlot.onRemove.RemoveAllListeners();
                bagSlot.onRemove.AddListener(()=> OnRemoveItemBag(bagSlot));
                itemBagList.Add(bagSlot);
            }
        }
        else
        {
            for (int i = 0; i < CurrentBagList.Count; i++)
            {
                if (i <= itemBagList.Count - 1)
                {
                    itemBagList[i].SetData(CurrentBagList[i],SelectedBagItem);
                    int idx = i;
                    itemBagList[i].onRemove.RemoveAllListeners();
                    itemBagList[i].onRemove.AddListener(()=> OnRemoveItemBag(itemBagList[idx]));
                }
                else
                {
                    var obj = AssetsManager.Instance.Instantiate(AssetKeys.ItemBagSlotPath);
                    obj.transform.SetParent(itemScrollRect.content);
                    obj.transform.localScale = Vector3.one;
                    ItemBagSlot bagSlot = obj.GetComponent<ItemBagSlot>();
                    bagSlot.Init();
                    bagSlot.SetData(CurrentBagList[i],SelectedBagItem);
                    bagSlot.onRemove.RemoveAllListeners();
                    bagSlot.onRemove.AddListener(()=> OnRemoveItemBag(bagSlot));
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

        if (selectedItemSlot != null)
        {
            selectedItemSlot.ActiveSelectedNumber(false);
            selectedItemSlotNumber = 0;
            selectedItemSlot = null;
            CalculateTotalSellPrice();
        }

        
    }

    private void SelectedBagItem(ItemBagSlot bagSlot)
    {
        if (selectedItemSlot == bagSlot)
        {
            selectedItemSlot.ActiveSelectedNumber(true);
            if (selectedItemSlotNumber + 1 <= selectedItemSlot.itemBag.itemAmount)
            {
                selectedItemSlotNumber++;
                selectedItemSlot.ShowSelectedNumber(selectedItemSlotNumber);
            }

            CalculateTotalSellPrice();
            return;
        }


        if (bagSlot == null)
        {
            for (int i = 0; i < itemBagList.Count; i++)
            {
                itemBagList[i].SetSelected(false);
            }
            ShowSelectedItem(null);
            if (selectedItemSlot != null)
            {
                selectedItemSlot.onLongPress.RemoveAllListeners();
                selectedItemSlot = null;
            }

            
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
                    itemBagList[i].ActiveSelectedNumber(false);
                }
            }

            selectedItemSlot = bagSlot;
            selectedItemSlotNumber = 1;
            bagSlot.ActiveSelectedNumber(true);
            bagSlot.ShowSelectedNumber(1);
            bagSlot.onLongPress.AddListener(()=>SelectedBagItem(bagSlot));
            ShowSelectedItem(bagSlot.itemData);
        }
        CalculateTotalSellPrice();
    }
    
    private void OnRemoveItemBag(ItemBagSlot bagSlot)
    {
        if (bagSlot == selectedItemSlot)
        {
            selectedItemSlot.ActiveSelectedNumber(true);
            selectedItemSlotNumber--;
            if (selectedItemSlotNumber <= 0)
            {
                selectedItemSlot.ActiveSelectedNumber(false);
                selectedItemSlotNumber = 0;
                selectedItemSlot = null;
            }
            else
            {
                selectedItemSlot.ShowSelectedNumber(selectedItemSlotNumber);
            }
            CalculateTotalSellPrice();
        }
    }

    private void ShowSelectedItem(ItemData itemData)
    {
        if (itemData == null)
        {
            NullMask.gameObject.SetActive(true);
            return;
        }
        NullMask.gameObject.SetActive(false);
        selectedItemNameStringEvent.SetText("InventoryItem",itemData.Name);
        selectedItemDescStringEvent.SetText("InventoryItem",itemData.Desc);
    }
    
    private void OptionSortType(ItemSortType sortType)
    {
        _itemSortType = sortType;
        GenerateInventoryItem(CurrentBagList);
    }
    
    
    private List<ItemBag> ApplySort(List<ItemBag> itemBags)
    {
        switch (_itemSortType)
        {
            case ItemSortType.CreatTime:
                return  itemBags.OrderByDescending(x => x.CreateTime).ToList();
                break;
            case ItemSortType.Number:
                return itemBags.OrderByDescending(x => x.itemAmount).ToList();
                break;
            case ItemSortType.Quality:
                return itemBags.OrderByDescending(x =>InventoryManager.Instance.GetItemData(x.itemID).Quality).ToList();
                break;
        }
        return itemBags;
    }
    
    
    public void CalculateTotalSellPrice()
    {
        int price = 0;
        if (selectedItemSlot != null)
        {
            price += selectedItemSlotNumber * selectedItemSlot.itemData.Shop;
            SellButton.interactable = true;
        }
        else
        {
            SellButton.interactable = false;
        }

        selectedSellItemPriceStringEvent.SetVar("value",price);
    }

    public void SellItem()
    {
        if (selectedItemSlot != null)
        {
            int price = selectedItemSlotNumber * selectedItemSlot.itemData.Shop;
            long itemID = selectedItemSlot.itemBag.itemID;
            int number = selectedItemSlotNumber;
            selectedItemSlot.ActiveSelectedNumber(false);
            selectedItemSlotNumber = 0;
            selectedItemSlot = null;
            InventoryManager.Instance.ConsumeItem(itemID, number);
            
            CalculateTotalSellPrice();
            GameDataManager.Instance.AddProperty(PropertyType.Gold,price);
            SaveGameManager.Instance.Save();
        }
    }
}

