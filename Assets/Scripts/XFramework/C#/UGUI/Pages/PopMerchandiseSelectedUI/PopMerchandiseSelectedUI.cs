using System.Collections.Generic;
using Sirenix.Utilities;
using UnityEngine;
using XFramework;

public partial class PopMerchandiseSelectedUI : UIBase
{
    private List<MerchandiseSlot> merchandiseSlots = new List<MerchandiseSlot>();
    
    private List<MerchandiseSlot>  selectedSlots = new List<MerchandiseSlot>();
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(closeButton,Close,"");
        Bind(saveButton,OnClickSave,"");
        Bind(autoAddButton,AutoAddSelectedRuntimeItem,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        InventoryManager.Instance.RegisterItemRuntimeChangeCallBack(CreateRuntimeItemBag);
        ExhibitionManager.Instance.RegisterSelectedFactoryUpdate(SelectedFactoryUpdate);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        InventoryManager.Instance.UnregisterItemRuntimeChangeCallBack(CreateRuntimeItemBag);
        ExhibitionManager.Instance.UnRegisterSelectedFactoryUpdate(SelectedFactoryUpdate);
        foreach (var slot in merchandiseSlots)
        {
            slot.SetSelected(false);
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        merchandiseSlots.Clear();
        selectedSlots.Clear();
    }
    
    private void CreateRuntimeItemBag(List<RuntimeItemInfo> itemInfos)
    {
        foreach (var slot in merchandiseSlots)
        {
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        merchandiseSlots.Clear();
        selectedSlots.Clear();
        
        foreach (var runtimeItemInfo in itemInfos)
        {
            if (runtimeItemInfo is FactoryMerchandiseItemInfo factoryItemInfo)
            {
                var obj  = AssetsManager.Instance.Instantiate(AssetKeys.MerchandiseSlotPath);
                obj.transform.SetParent(scrollView.content);
                obj.transform.localScale = Vector3.one;
                
                var slot = obj.GetComponent<MerchandiseSlot>();
                slot.Init();
                slot.SetData(factoryItemInfo,OnSelectedRuntimeItem,OnSubRuntimeItemSelected);
                merchandiseSlots.Add(slot);
            }
        }
    }

    private void SelectedFactoryUpdate(List<FactoryMerchandiseItemInfo> itemInfos)
    {
        foreach (var selectedSlot in selectedSlots)
        {
            selectedSlot.SetSelected(false);
        }
        selectedSlots.Clear();
        foreach (var itemInfo in itemInfos)
        {
            foreach (var slot in merchandiseSlots)
            {
                if (slot.FactoryItemInfo.ID == itemInfo.ID)
                {
                    slot.SetSelected(true);
                    selectedSlots.Add(slot);
                }
            }
        }
        RefreshTotal();
    }

    private void AutoAddSelectedRuntimeItem()
    {
        ExhibitionManager.Instance.AutoAddFactoryList();
    }

    private void OnSelectedRuntimeItem(MerchandiseSlot slot)
    {
        if (selectedSlots.Count >= 10)
        {
            Debug.Log("类型超过10种了!!!!");
            return;
        }

        if (!selectedSlots.Contains(slot))
        {
            selectedSlots.Add(slot);
            slot.SetSelected(true);
            RefreshTotal();
        }
    }

    private void OnSubRuntimeItemSelected(MerchandiseSlot slot)
    {
        if (selectedSlots.Contains(slot))
        {
            slot.SetSelected(false);
            selectedSlots.Remove(slot);
            RefreshTotal();
        }
       
    }

    private void RefreshTotal()
    {
        int totalCount = 0;
        foreach (var slot in selectedSlots)
        {
            totalCount += slot.FactoryItemInfo.Count;
        }

        totalValTex.SetVar("TypeNumber",selectedSlots.Count);
        totalValTex.SetVar("Number",totalCount);
    }

    private void OnClickSave()
    {
        List<FactoryMerchandiseItemInfo> result = new List<FactoryMerchandiseItemInfo>();

        foreach (var slot in selectedSlots)
        {
            result.Add(slot.FactoryItemInfo);
        }


        ExhibitionManager.Instance.SetSelectedFactory(result);
        
        Close();
    }
}

