using UnityEngine;

public class SupermarketUI : BaseShopUI
{
    protected override void BindClothEvent()
    {
        GameDataManager.Instance.BindPlayerDataChange(UpdatePlayerDataUI);
        ShopManager.Instance.BindSuperMarkShopChange(GenerateShopItems);
        InventoryManager.Instance.RegisterAllItemChange(GenerateInventoryItem);
    }

    protected override void UnBindShopEvent()
    {
        GameDataManager.Instance.UnBindPlayerDataChange(UpdatePlayerDataUI);
        ShopManager.Instance.UnBindSuperMarkShopChange(GenerateShopItems);
        InventoryManager.Instance.UnregisterAllItemChange(GenerateInventoryItem);
        base.UnBindShopEvent();
    }
}
