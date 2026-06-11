using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class ActionCGPage : UIBase
{
    private const string PhotoSlotPath = "Assets/AddressableAssets/Remote/Prefabs/UGUI/PhotoAlbumUI/PhotoSlotUI.prefab";
    private ScrollRect _scrollRect;
    private List<PhotoSlotUI> PhotoSlots;
    private PhotoSlotUI maxPhotoSlotUI;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _scrollRect = Get<ScrollRect>("UIMask/ScrollRect");
        maxPhotoSlotUI = Get<PhotoSlotUI>("UIMask/CGItem_Panel/PhotoSlotUI");
        maxPhotoSlotUI.Init();
        CreatPhotoSlotUI();
    }

    private void CreatPhotoSlotUI()
    {
        PhotoSlots = new List<PhotoSlotUI>();
       
        foreach (var data in GameDataManager.Instance.PhotoAlbumData.ActionCGDataList)
        {
            var labelObj =  AssetsManager.Instance.Instantiate(PhotoSlotPath);
            labelObj.transform.SetParent(_scrollRect.content);
            labelObj.transform.localPosition = Vector3.zero;
            labelObj.transform.localScale = Vector3.one;
            
            var slotUI = labelObj.GetComponent<PhotoSlotUI>();
            slotUI.Init();
            slotUI.SetData(data,SelectedPhotoSlotUI);
            
            PhotoSlots.Add(slotUI);
        }
    }
    
    private void SelectedPhotoSlotUI(PhotoSlotUI photoSlotUI)
    {
        foreach (PhotoSlotUI item in PhotoSlots)
        {
            item.SetSelected(item == photoSlotUI);
        }
        
        maxPhotoSlotUI.SetData(photoSlotUI.currentData,null);
    }
}
