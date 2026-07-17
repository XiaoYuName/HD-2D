using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class PopMerchandiseSelectedUI : UIBase
{
    private List<MerchandiseSlot> merchandiseSlots = new List<MerchandiseSlot>();
    
    private List<MerchandiseSlot> selectedSlots = new List<MerchandiseSlot>();
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(closeButton,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        InventoryManager.Instance.RegisterItemRuntimeChangeCallBack(CreateRuntimeItemBag);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        InventoryManager.Instance.UnregisterItemRuntimeChangeCallBack(CreateRuntimeItemBag);
        foreach (var slot in merchandiseSlots)
        {
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        merchandiseSlots.Clear();
    }
    
    private void CreateRuntimeItemBag(List<RuntimeItemInfo> itemInfos)
    {
        foreach (var slot in merchandiseSlots)
        {
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        merchandiseSlots.Clear();
        
        foreach (var runtimeItemInfo in itemInfos)
        {
            if (runtimeItemInfo is FactoryMerchandiseItemInfo factoryItemInfo)
            {
                var obj  = AssetsManager.Instance.Instantiate(AssetKeys.MerchandiseSlotPath);
                obj.transform.SetParent(scrollView.content);
                obj.transform.localScale = Vector3.one;
                
                var slot = obj.GetComponent<MerchandiseSlot>();
                slot.Init();
                slot.SetData(factoryItemInfo,OnSelectedRuntimeItem);
                merchandiseSlots.Add(slot);
            }
        }
    }


    private void OnSelectedRuntimeItem(MerchandiseSlot slot)
    {
        if (!selectedSlots.Contains(slot))
        {
            slot.SetSelected(true);
            selectedSlots.Add(slot);
        }
    }


}
