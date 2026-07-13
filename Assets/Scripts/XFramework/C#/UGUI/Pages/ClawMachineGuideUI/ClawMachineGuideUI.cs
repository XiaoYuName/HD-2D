using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using XFramework;

/// <summary>
/// 娃娃机图鉴UI
/// </summary>
public partial class ClawMachineGuideUI : UIBase
{
    private List<GuideBag>  _dollGuideDataList;
    
    private Dictionary<long, ClawMachineGuidItemSlot> _dollCatalogDataDict;
    
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
        CreatDollGruid();
        InventoryManager.Instance.RegisterMaterialTypeChangeCallBack(ItemMaterialType.Doll, UpdateDollItemSlotData);
        InventoryManager.Instance.RegisterMaterialTypeChangeCallBack(ItemMaterialType.FigureModel, UpdateDollItemSlotData);
        
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        InventoryManager.Instance.UnregisterMaterialTypeChangeCallBack(ItemMaterialType.Doll,UpdateDollItemSlotData);
        InventoryManager.Instance.UnregisterMaterialTypeChangeCallBack(ItemMaterialType.FigureModel, UpdateDollItemSlotData);
        foreach (var id in _dollCatalogDataDict.Keys)
        {
            _dollCatalogDataDict[id].Release();
            AssetsManager.Instance.FreeGameObject(_dollCatalogDataDict[id].gameObject);
        }
        _dollCatalogDataDict.Clear();
    }

    private void CreatDollGruid()
    {
        _dollCatalogDataDict = new Dictionary<long, ClawMachineGuidItemSlot>();
        int index = 0;
        foreach (DollCatalogData dollCatalogData in GuideManager.Instance.GetDollCatalogData())
        {
            index++;
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.ClawMachineGuidItemSlotPath);
            obj.transform.SetParent(itemContent);
            obj.transform.localScale = Vector2.one;

            var itemSlot = obj.transform.GetComponent<ClawMachineGuidItemSlot>();
            itemSlot.Init();
            itemSlot.SetData(dollCatalogData);
            itemSlot.SetIndexLabel(index);
            itemSlot.OnClick += SelectedDollItem;
            _dollCatalogDataDict.Add(dollCatalogData.ItemID,itemSlot);
        }
       
        
    }

    private void UpdateDollItemSlotData(List<ItemInfo> dollBags)
    {
        foreach (var dollBag in dollBags)
        {
            if (_dollCatalogDataDict.ContainsKey(dollBag.ID))
            {
                _dollCatalogDataDict[dollBag.ID].UpdateData(dollBag);
            }
        }
        SelectedDollItem(_dollCatalogDataDict.Values.First());
    }

    private void SelectedDollItem(ClawMachineGuidItemSlot slot)
    {
        if (!InventoryManager.Instance.HasItemUnlock(slot.ItemData.ID))
        {
            dollIcon.sprite =
                AssetsManager.Instance.LoadAssets<Sprite>(
                    GamePathTools.CombinationDollImagePath(slot.DollCatalogData.UlockImageName));
        }
        else
        {
            dollIcon.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationItemIconPath(slot.ItemData.IconName));
        }


        name.SetText(slot.ItemData.NameKey.Table,slot.ItemData.NameKey.Value);
        desc.SetText(slot.ItemData.DescKey.Table,slot.ItemData.DescKey.Value);
    }
}
