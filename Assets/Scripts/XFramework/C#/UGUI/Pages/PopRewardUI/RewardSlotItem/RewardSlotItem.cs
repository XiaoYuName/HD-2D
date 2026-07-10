using UnityEngine;
using XFramework;

public partial class RewardSlotItem : UIBase
{
    public ItemData ItemData { get; set; }
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }


    public void SetData(ItemInfo stack)
    {
        this.ItemData = InventoryManager.Instance.GetItemData(stack.ID);
        if (ItemData == null)
        {
            Debug.LogError("没有找到对应物品的ItemData定义~");
            return;
        }

        icon.sprite =
            AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationItemIconPath(ItemData.IconName));
        itemName.SetText(ItemData.NameKey.Table,ItemData.NameKey.Value);
        itemDesc.SetText(ItemData.DescKey.Table,ItemData.DescKey.Value);
        itemCount.text = $"X{stack.Count}";
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        if (ItemData != null)
        {
            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationItemIconPath(ItemData.IconName));
            ItemData = null;
        }
    }
}
