using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class PopRewardUI : UIBase
{
    private List<RewardSlotItem>  rewardSlotItems;
    private bool isShow;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(button,Close,"");
    }


    public void ShowReward(List<ItemStack> reward)
    {
        if (!isShow)
        {
            isShow = true;
            StartCoroutine(ShowRewardAsync(reward));
        }
    }

    public void ShowReward(ItemStack reward)
    {
        if (!isShow)
        {
            isShow = true;
            List<ItemStack> stack = new List<ItemStack>();
            stack.Add(reward);
            StartCoroutine(ShowRewardAsync(stack));
        }
    }

    public void ShowReward(List<ShopItemBag> reward)
    {
        List<ItemStack> stack = new List<ItemStack>();
        foreach (var item in reward)
        {
            ItemData itemData = InventoryManager.Instance.GetItemData(item.ItemID);
            ItemStack newStack = new ItemStack(item.ItemID,item.ItemNumber,itemData.ItemType);
            stack.Add(newStack);
        }

        if (!isShow)
        {
            isShow = true;
            StartCoroutine(ShowRewardAsync(stack));
        }

    }

    private IEnumerator ShowRewardAsync(List<ItemStack> reward)
    {
        rewardSlotItems = new List<RewardSlotItem>();
        foreach (ItemStack stack in reward)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.RewardSlotItemPath);
            obj.transform.SetParent(scrollView.content);
            obj.transform.localScale = Vector3.one;
            obj.transform.localPosition = Vector3.zero;

            var slot = obj.GetComponent<RewardSlotItem>();
            slot.Init();
            slot.SetData(stack);
            slot.Open();
            rewardSlotItems.Add(slot);
            yield return new WaitForSeconds(0.25f);
            
        }

        isShow = false;
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        foreach (var rewardSlotItem in rewardSlotItems)
        {
            rewardSlotItem.Close();
            AssetsManager.Instance.FreeGameObject(rewardSlotItem.gameObject);
        }
        rewardSlotItems.Clear();
        
        
        base.Close();
    }
}
