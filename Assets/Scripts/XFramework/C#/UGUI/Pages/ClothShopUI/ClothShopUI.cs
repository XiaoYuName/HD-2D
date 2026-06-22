using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 布料商店
/// </summary>
public class ClothShopUI : UIBase
{
    private LocalizeStringEvent currentGoldStringEvent;
    private Button closeButton;

    private ScrollRect shopItemScrollRect;
    
    
    private List<ClothShopItemSlot>  ShopItemBags = new List<ClothShopItemSlot>();
    
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        currentGoldStringEvent = Get<LocalizeStringEvent>("UIMask/Panel/Top/CurrentGoldValue/ValueTex");
        closeButton = Get<Button>("UIMask/Panel/CloseButton");
        shopItemScrollRect = Get<ScrollRect>("UIMask/Panel/ItemFarme/ShopFarme/Scroll View");
        
        Bind(closeButton,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.BindUserChange(UpdateUserUI);
        ShopManager.Instance.BindClothShopChange(GenerateShopItems);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnBindUserChange(UpdateUserUI);
        ShopManager.Instance.UnBindClothShopChange(GenerateShopItems);
    }


    private void GenerateShopItems(List<ClothShopData> shopItems)
    {
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


    private void UpdateUserUI(User user)
    {
        currentGoldStringEvent.SetVar("value",user.GoldNumber);
    }
}

