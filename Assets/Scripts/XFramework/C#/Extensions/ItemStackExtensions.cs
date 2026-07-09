using UnityEngine;
using XFramework;

public static class ItemStackExtensions
{
    public static ItemData GetItemData(this ItemStack stack)
    {
        if (stack == null)
        {
            Debug.LogWarning("ItemStack is null");
            return null;
        }

        return InventoryManager.Instance.GetItemData(stack.ID);
    }
}
