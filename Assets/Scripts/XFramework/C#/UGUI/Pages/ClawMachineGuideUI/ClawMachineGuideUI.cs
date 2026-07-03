using System.Collections.Generic;
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
        CreatDollGruid();
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GuideManager.Instance.RegisterDollGuidChange(UpdateDollItemSlotData);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GuideManager.Instance.UnregisterDollGuidChange(UpdateDollItemSlotData);
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
            _dollCatalogDataDict.Add(dollCatalogData.ID,itemSlot);
        }
       
    }

    private void UpdateDollItemSlotData(List<GuideBag> dollBags)
    {
        foreach (var dollBag in dollBags)
        {
            if (_dollCatalogDataDict.ContainsKey(dollBag.Id))
            {
                _dollCatalogDataDict[dollBag.Id].UpdateData(dollBag);
            }
        }
    }
}
