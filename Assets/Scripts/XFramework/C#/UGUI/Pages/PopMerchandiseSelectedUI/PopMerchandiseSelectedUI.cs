using System.Collections.Generic;
using Sirenix.Utilities;
using UnityEngine;
using XFramework;

public partial class PopMerchandiseSelectedUI : UIBase
{
    private List<MerchandiseSlot> merchandiseSlots = new List<MerchandiseSlot>();
    
    private Dictionary<MerchandiseSlot,OnSelectedFactoryMerchandiseItem>  selectedSlots = 
        new Dictionary<MerchandiseSlot,OnSelectedFactoryMerchandiseItem>();
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(closeButton,Close,"");
        Bind(saveButton,OnClickSave,"");
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
                
                selectedSlots.Add(slot,new OnSelectedFactoryMerchandiseItem()
                {
                    ItemInfo = factoryItemInfo,
                    Count = 0,
                });
            }
        }
    }

    private void SelectedFactoryUpdate(List<FactoryMerchandiseItemInfo> itemInfos)
    {
        int totalCount = 0;
        foreach (var itemInfo in itemInfos)
        {
            foreach (var key in selectedSlots.Keys)
            {
                if (selectedSlots[key].ItemInfo.ID == itemInfo.ID)
                {
                    selectedSlots[key].Count = itemInfo.Count;
                    key.SetSelectedNumber(selectedSlots[key].Count,selectedSlots[key].ItemInfo.Count);
                    key.SetSelected(itemInfo.Count > 0);
                    totalCount += itemInfo.Count;
                }
            }
        }

        RefreshTotal();
    }


    private void OnSelectedRuntimeItem(MerchandiseSlot slot)
    {
        List<MerchandiseSlot> TypeSlots = new List<MerchandiseSlot>();
        selectedSlots.ForEach(temp =>
        {
            if (temp.Value.Count > 0)
            {
                TypeSlots.Add(temp.Key);
            }
        });

        if (!TypeSlots.Contains(slot))
        {
            if (TypeSlots.Count >= 10)
            {
                Debug.Log("类型超过10种了!!!!");
                return;
            }
        }

        if (selectedSlots.ContainsKey(slot))
        {
            selectedSlots[slot].Count = Mathf.Min(selectedSlots[slot].Count +1,selectedSlots[slot].ItemInfo.Count);
            slot.SetSelected(true);
            slot.SetSelectedNumber(selectedSlots[slot].Count,selectedSlots[slot].ItemInfo.Count);
        }
        RefreshTotal();
    }

    private void OnSubRuntimeItemSelected(MerchandiseSlot slot)
    {
        if (selectedSlots.ContainsKey(slot))
        {
            selectedSlots[slot].Count--;
            if (selectedSlots[slot].Count <= 0)
            {
                slot.SetSelected(false);
                selectedSlots[slot].Count = 0;
            }
            slot.SetSelectedNumber(selectedSlots[slot].Count,selectedSlots[slot].ItemInfo.Count);
        }
        RefreshTotal();
    }

    private void RefreshTotal()
    {
        int typeNumber = 0;
        int totalCount = 0;
        foreach (var slot in selectedSlots.Keys)
        {
            if (selectedSlots[slot].Count > 0)
            {
                typeNumber++;
                totalCount += selectedSlots[slot].Count;
            }
        }

        totalValTex.SetVar("TypeNumber",typeNumber);
        totalValTex.SetVar("Number",totalCount);
    }

    private void OnClickSave()
    {
        List<FactoryMerchandiseItemInfo> result = new List<FactoryMerchandiseItemInfo>();

        foreach (var slot in selectedSlots.Keys)
        {
            if (selectedSlots[slot].Count > 0)
            {
                result.Add(new FactoryMerchandiseItemInfo(selectedSlots[slot].ItemInfo.ID,selectedSlots[slot].Count
                ,selectedSlots[slot].ItemInfo.FrameItemId,selectedSlots[slot].ItemInfo.PaintingItemId));
            }
        }


        ExhibitionManager.Instance.SetSelectedFactory(result);
        
        Close();
    }


}

[System.Serializable]
public class OnSelectedFactoryMerchandiseItem
{
    public FactoryMerchandiseItemInfo ItemInfo;
    public int Count;
}

