using System;
using System.Collections.Generic;
using XFramework;

public partial class PopSelectedPictureUI : UIBase
{
    private List<ItemInfo> selectedItems = new List<ItemInfo>();
    private Action<List<ItemInfo>> OnItemSelected;
    
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
        InventoryManager.Instance.RegisterMaterialTypeChangeCallBack(ItemMaterialType.Painting,CreatPaintingSlotUI);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        InventoryManager.Instance.UnregisterMaterialTypeChangeCallBack(ItemMaterialType.Painting,CreatPaintingSlotUI);
        
    }

    private void CreatPaintingSlotUI(List<ItemInfo> itemInfos)
    {
        
    }

    public void RegisterOnSelected(Action<List<ItemInfo>> callback)
    {
        OnItemSelected =  callback;
    }
    
}
