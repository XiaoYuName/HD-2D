using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 商店 UI 基类。
/// 负责通用的购买、出售、筛选、结算、库存显示逻辑。
/// 子类只需要提供具体商店的数据监听、数据写回和商品配置查询方式。
/// </summary>
public abstract class BaseShopUI : UIBase
{
    // 顶部金币和购买合计显示。
    protected LocalizeStringEvent currentGoldStringEvent;
    protected LocalizeStringEvent allPriceStringEvent;
    protected Button closeButton;
    protected Button sellCloseButton;
    protected Button BuyAllButton;

    // 购买页的类型筛选、商品列表、购物车列表。
    protected ScrollRect shopItemTypeScrollRect;
    protected ScrollRect shopItemScrollRect;
    protected ScrollRect shopBuyItemScrollRect;
    
    // 当前购买页生成出来的 UI 槽位。
    protected List<ClothShopItemSlot>  ShopItemBags = new List<ClothShopItemSlot>();
    protected LabelButton AllItemTypeButton;
    protected Dictionary<ItemType, LabelButton> itemTypeButtonList;
    protected List<ClothBuyItemSlot> buyItemSlotList = new List<ClothBuyItemSlot>();
    protected LocalSelectedData _localSelectedData;
    
    // 当前商店的库存快照。购买时先改这份数据，结算成功后再写回 ShopManager。
    protected List<ShopItemBag> _shopItems = new List<ShopItemBag>();
    
    // 买入/卖出模式切换相关 UI。
    protected ShopMode  _shopMode;
    protected RectTransform BuyRect;
    protected RectTransform SellRect;
    protected Button OptionSellButton;
    protected Button OptionBuyButton;
    
    // 出售页的玩家背包数据和 UI 槽位。
    protected List<ItemBag> CurrentBagList = new List<ItemBag>();
    protected List<ItemBagSlot> itemBagList = new List<ItemBagSlot>();
    protected ScrollRect itemScrollRect;

    // 默认复用布料商店的 prefab。子类如果有专属 UI，可以 override 这些路径。
    protected virtual string ItemTypeButtonPath => AssetKeys.ClothItemLabelButtonPath;
    protected virtual string ShopItemSlotPath => AssetKeys.ClothShopItemSlotPath;
    protected virtual string BuyItemSlotPath => AssetKeys.ClothBuyItemSlotPath;


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
    
    // 出售页当前选中的背包物品和数量。
    protected LocalizeStringEvent selectedItemNameStringEvent;
    protected LocalizeStringEvent selectedItemDescStringEvent;
    protected LocalizeStringEvent selectedSellItemPriceStringEvent;
    protected RectTransform NullMask;
    protected ItemBagSlot selectedItemSlot;
    protected CustomDropdownUI _customDropdownUI;
    protected int selectedItemSlotNumber;
    protected ItemSortType _itemSortType;
    protected Button SellButton;
    
    
    /// <summary>
    /// 初始化商店 UI 引用、按钮事件和默认状态，一般不需要手动调用。
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
        sellCloseButton = Get<Button>("UIMask/Panel/SellRect/CloseButton");
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
        Bind(sellCloseButton, Close, "");
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
    /// 打开商店时绑定数据事件，并默认进入购买页。
    /// </summary>
    public override void Open()
    {
        base.Open();
        RegisterShopEvent();
        OptionType(_localSelectedData);
        OptionShowMode(ShopMode.Buy);
        PlayerInputManager.Instance.OnRightClick += Close;
    }

    /// <summary>
    /// 关闭商店时清空未结算购物车并解绑事件，避免下次打开残留旧状态。
    /// </summary>
    public override void Close()
    {
        ClearBuyItems();
        base.Close();
        UnregisterShopEvent();
        PlayerInputManager.Instance.OnRightClick -= Close;
    }

    #region BindShopType

    /// <summary>
    /// 绑定玩家数据、商店库存和背包变化。
    /// 子类只负责商店库存变化事件，其他通用事件由基类统一处理。
    /// </summary>
    protected virtual void RegisterShopEvent()
    {
        GameDataManager.Instance.RegisterPlayerDataChange(UpdatePlayerDataUI);
        RegisterShopChange(GenerateShopItems);
        InventoryManager.Instance.RegisterAllItemChange(GenerateInventoryItem);
    }

    /// <summary>
    /// 解绑商店相关事件。和 RegisterShopEvent 保持成对出现，避免 UI 重复刷新或泄漏回调。
    /// </summary>
    protected virtual void UnregisterShopEvent()
    {
        GameDataManager.Instance.UnregisterPlayerDataChange(UpdatePlayerDataUI);
        UnregisterShopChange(GenerateShopItems);
        InventoryManager.Instance.UnregisterAllItemChange(GenerateInventoryItem);
    }

    /// <summary>
    /// 绑定具体商店的库存变化事件。
    /// 例如布料商店绑定 ClothShop，超市绑定 SuperMarketShop。
    /// </summary>
    protected abstract void RegisterShopChange(Action<List<ShopItemBag>> callback);

    /// <summary>
    /// 解绑具体商店的库存变化事件。
    /// </summary>
    protected abstract void UnregisterShopChange(Action<List<ShopItemBag>> callback);

    /// <summary>
    /// 结算成功后，把基类计算后的库存写回具体商店。
    /// </summary>
    protected abstract void SetShopItems(List<ShopItemBag> shopItems);

    /// <summary>
    /// 根据物品 ID 查询当前商店的商品配置。
    /// 不同商店可以来自不同 Luban 表，但返回统一的 ShopGoodsData 给基类使用。
    /// </summary>
    protected abstract ShopGoodsData GetShopGoodsData(long itemID);

    /// <summary>
    /// 槽位 UI 通过这个方法拿商品配置，避免槽位直接依赖某一张具体商店表。
    /// </summary>
    public ShopGoodsData GetGoodsData(long itemID)
    {
        return GetShopGoodsData(itemID);
    }

    #endregion

    /// <summary>
    /// 生成购买页左侧的物品类型筛选按钮。
    /// </summary>
    protected virtual void GenerateItemTypeButtons()
    {
        itemTypeButtonList = new Dictionary<ItemType, LabelButton>();
        var allObj = AssetsManager.Instance.Instantiate(ItemTypeButtonPath);
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
            var obj = AssetsManager.Instance.Instantiate(ItemTypeButtonPath);
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

    /// <summary>
    /// 根据商店库存刷新购买页商品列表。
    /// 这里会复制一份库存到 _shopItems，购买过程只修改本地快照，结算后再写回真实数据。
    /// </summary>
    protected virtual void GenerateShopItems(List<ShopItemBag> shopItems)
    {
        shopItems ??= new List<ShopItemBag>();
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
            ShopItemBags.Clear();
            return;
        }
        
        if (ShopItemBags.Count <= 0)
        {
            for (int i = 0; i < shopItems.Count; i++)
            {
                var obj = AssetsManager.Instance.Instantiate(ShopItemSlotPath);
                obj.transform.SetParent(shopItemScrollRect.content);
                obj.transform.localScale = Vector3.one;
                ClothShopItemSlot bagSlot = obj.GetComponent<ClothShopItemSlot>();
                bagSlot.Init();
                bagSlot.SetData(shopItems[i],this);
                ShopItemBags.Add(bagSlot);
            }
            return;
        }

        for (int i = 0; i < shopItems.Count; i++)
        {
            if (i <= ShopItemBags.Count - 1)
            {
                ShopItemBags[i].Release();
                ShopItemBags[i].SetData(shopItems[i],this);
            }
            else
            {
                var obj = AssetsManager.Instance.Instantiate(ShopItemSlotPath);
                obj.transform.SetParent(shopItemScrollRect.content);
                obj.transform.localScale = Vector3.one;
                ClothShopItemSlot bagSlot = obj.GetComponent<ClothShopItemSlot>();
                bagSlot.Init();
                bagSlot.SetData(shopItems[i],this);
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


    /// <summary>
    /// 从商店商品槽位点击“加号”时进入购买流程。
    /// </summary>
    public virtual void AddBuyItem(ClothShopItemSlot bagSlot)
    {
        AddBuyItem(bagSlot.ShopItemData);
    }

    /// <summary>
    /// 从购物车槽位点击“加号”时继续增加购买数量。
    /// </summary>
    public virtual void AddBuyItem(ClothBuyItemSlot  bagSlot)
    {
        AddBuyItem(bagSlot.ShopItemData);
    }

    /// <summary>
    /// 增加一个待购买商品。
    /// 会先扣减本地商店库存，再创建或刷新购物车槽位。
    /// </summary>
    protected virtual void AddBuyItem(ShopGoodsData data)
    {
        if (data == null)
        {
            return;
        }

        int shopIndex = _shopItems.FindIndex(t => t.ItemID == data.ItemID);
        if (shopIndex >= 0)
        {
            if (_shopItems[shopIndex].ItemNumber > 0)
            {
                int buyIndex = buyItemSlotList.FindIndex(temp => temp.ShopItemData != null && temp.ShopItemData.ItemID == data.ItemID);
                if (buyIndex >= 0)
                {
                    // 购物车已有该商品，只增加选择数量。
                    var shopItemBag = buyItemSlotList[buyIndex].ItemBag;
                    if (shopItemBag == null)
                    {
                        return;
                    }
                    _shopItems[shopIndex].ItemNumber--;
                    shopItemBag.ItemNumber++;
                    buyItemSlotList[buyIndex].SetData(shopItemBag,this);
                }
                else
                {
                    // 购物车还没有该商品，创建一个新的购物车槽位。
                    _shopItems[shopIndex].ItemNumber--;
                    var obj = AssetsManager.Instance.Instantiate(BuyItemSlotPath);
                    obj.transform.SetParent(shopBuyItemScrollRect.content);
                    obj.transform.localScale = Vector3.one;
                    var buy = obj.GetComponent<ClothBuyItemSlot>();
                    buy.Init();
                    ShopItemBag clothShopItemBag =  new ShopItemBag()
                    {
                        ItemID =  data.ItemID,
                        ItemNumber = 1,
                    };
                    buy.SetData(clothShopItemBag,this);
                    buyItemSlotList.Add(buy);
                }
            
            }
            GenerateShopItems(_shopItems);
            CalculateTotalPrice();
        }
    }


    /// <summary>
    /// 减少一个待购买商品。
    /// 会把数量还回本地商店库存；购物车数量归零时释放槽位。
    /// </summary>
    public virtual void RemoveBuyItem(ShopGoodsData data)
    {
        if (data == null)
        {
            return;
        }

        int shopIndex = _shopItems.FindIndex(t => t.ItemID == data.ItemID);
        int buyIndex = buyItemSlotList.FindIndex(t => t.ShopItemData != null && t.ShopItemData.ItemID == data.ItemID);
        if (shopIndex >= 0 && buyIndex >= 0)
        {
            var shopItemBag =  buyItemSlotList[buyIndex].ItemBag;
            if (shopItemBag == null)
            {
                return;
            }
            _shopItems[shopIndex].ItemNumber++;
            shopItemBag.ItemNumber--;
            if (shopItemBag.ItemNumber <= 0)
            {
                buyItemSlotList[buyIndex].Release();
                AssetsManager.Instance.FreeGameObject(buyItemSlotList[buyIndex].gameObject);
                buyItemSlotList.RemoveAt(buyIndex);
            }
            else
            {
                buyItemSlotList[buyIndex].SetData(shopItemBag,this);
            }
            GenerateShopItems(_shopItems);
            CalculateTotalPrice();
        }
    }

    /// <summary>
    /// 从商店商品槽位点击“减号”时减少购买数量。
    /// </summary>
    public virtual void RemoveBuyItem(ClothShopItemSlot bagSlot)
    {
        RemoveBuyItem(bagSlot.ShopItemData);
    }

    /// <summary>
    /// 从购物车槽位点击“减号”时减少购买数量。
    /// </summary>
    public virtual void RemoveBuyItem(ClothBuyItemSlot bagSlot)
    {
        RemoveBuyItem(bagSlot.ShopItemData);
    }


    /// <summary>
    /// 计算购物车总价，并根据玩家金币数量控制结算按钮是否可点击。
    /// </summary>
    protected virtual void CalculateTotalPrice()
    {
        int price = 0;
        foreach (var bagSlot in buyItemSlotList)
        {
            if (bagSlot.ShopItemData == null || bagSlot.ItemBag == null)
            {
                continue;
            }
            price += bagSlot.ItemBag.ItemNumber * bagSlot.ShopItemData.Price ;
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
    /// 结算购物车中所有商品。
    /// 成功后扣金币、加背包、写回商店库存并保存；失败只弹出金币不足提示。
    /// </summary>
    protected virtual void SettlementShop()
    {
        var price = buyItemSlotList
            .Where(t => t.ShopItemData != null && t.ItemBag != null)
            .Sum(t => t.ItemBag.ItemNumber * t.ShopItemData.Price);
        if (price <= 0)
        {
            return;
        }

        if (GameDataManager.Instance.GetProperty(PropertyType.Gold).Value >= price)
        {
            GameDataManager.Instance.RemoveProperty(PropertyType.Gold,price);
            foreach (var bagSlot in buyItemSlotList)
            {
                if (bagSlot.ShopItemData != null && bagSlot.ItemBag != null)
                {
                    InventoryManager.Instance.AddItem(bagSlot.ItemBag.ItemID,bagSlot.ItemBag.ItemNumber);
                }
            }
            ClearBuyItems();
            // 只有结算成功才把本地库存快照写回 ShopManager。
            SetShopItems(_shopItems);
            UIUtility.ShowPopWindow(commTip,successContent,okButton);
            SaveGameManager.Instance.Save();
        }
        else
        {
            UIUtility.ShowPopWindow(commTip,errorContent,okButton);
        }


    }

    /// <summary>
    /// 清空购物车 UI 和缓存。
    /// 结算成功或关闭商店时调用，避免下次打开保留未结算商品。
    /// </summary>
    protected virtual void ClearBuyItems()
    {
        foreach (var bagSlot in buyItemSlotList)
        {
            bagSlot.Release();
            AssetsManager.Instance.FreeGameObject(bagSlot.gameObject);
        }
        buyItemSlotList.Clear();
        CalculateTotalPrice();
    }
    
    /// <summary>
    /// 玩家数据变化时刷新金币显示。
    /// </summary>
    protected virtual void UpdatePlayerDataUI(PlayerData user)
    {
        currentGoldStringEvent.SetVar("value",GameDataManager.Instance.GetProperty(PropertyType.Gold).Value);
    }
    
    /// <summary>
    /// 根据物品类型筛选购买列表和出售列表。
    /// All 表示显示所有类型。
    /// </summary>
    protected virtual void OptionType(LocalSelectedData selectedType)
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
    
    /// <summary>
    /// 切换购买/出售模式，并重置当前选择状态。
    /// </summary>
    protected virtual void OptionShowMode(ShopMode shopMode)
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

    /// <summary>
    /// 根据玩家背包数据刷新出售页列表。
    /// 背包变化时会重新排序、复用已有槽位，并清理多余槽位。
    /// </summary>
    protected virtual void GenerateInventoryItem(List<ItemBag> bags)
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

    /// <summary>
    /// 选择出售页背包物品。
    /// 重复点击同一物品会累加出售数量，直到达到背包拥有数量。
    /// </summary>
    protected virtual void SelectedBagItem(ItemBagSlot bagSlot)
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
    
    /// <summary>
    /// 出售页槽位减少选择数量时，同步更新当前选中数量和价格。
    /// </summary>
    protected virtual void OnRemoveItemBag(ItemBagSlot bagSlot)
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

    /// <summary>
    /// 显示出售页当前选中物品的名称和描述。
    /// </summary>
    protected virtual void ShowSelectedItem(ItemData itemData)
    {
        if (itemData == null)
        {
            NullMask.gameObject.SetActive(true);
            return;
        }
        NullMask.gameObject.SetActive(false);
        selectedItemNameStringEvent.SetText("InventoryItem", itemData.NameKey);
        selectedItemDescStringEvent.SetText("InventoryItem", itemData.DescKey);
    }
    
    /// <summary>
    /// 切换出售页背包排序方式。
    /// </summary>
    protected virtual void OptionSortType(ItemSortType sortType)
    {
        _itemSortType = sortType;
        GenerateInventoryItem(CurrentBagList);
    }
    
    /// <summary>
    /// 按当前排序规则返回背包列表。
    /// 注意这里返回新列表，不直接修改传入列表顺序。
    /// </summary>
    protected virtual List<ItemBag> ApplySort(List<ItemBag> itemBags)
    {
        switch (_itemSortType)
        {
            case ItemSortType.CreatTime:
                return  itemBags.OrderByDescending(x => x.CreateTime).ToList();
            case ItemSortType.Number:
                return itemBags.OrderByDescending(x => x.itemAmount).ToList();
            case ItemSortType.Quality:
                return itemBags.OrderByDescending(x =>InventoryManager.Instance.GetItemData(x.itemID).Quality).ToList();
        }
        return itemBags;
    }
    
    /// <summary>
    /// 计算出售页当前选中物品的总卖价，并控制出售按钮状态。
    /// </summary>
    protected virtual void CalculateTotalSellPrice()
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

    /// <summary>
    /// 出售当前选中的背包物品。
    /// 成功后消耗背包物品、增加金币并保存。
    /// </summary>
    protected virtual void SellItem()
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
