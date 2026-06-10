using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class ActionCGPage : UIBase
{
    private const string PhotoSlotPath = "Assets/AddressableAssets/Remote/Prefabs/UGUI/PhotoAlbumUI/PhotoSlotUI.prefab";
    private ScrollRect _scrollRect;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _scrollRect = Get<ScrollRect>("UIMask/ScrollRect");
        CreatPhotoSlotUI();
    }

    private void CreatPhotoSlotUI()
    {
        for (int i = 0; i < GameDataManager.Instance.PhotoAlbumData.ActionCGDataList.Count; i++)
        {
            var data = GameDataManager.Instance.PhotoAlbumData.ActionCGDataList[i];
            var labelObj =  AssetsManager.Instance.Instantiate(PhotoSlotPath);
            labelObj.transform.SetParent(_scrollRect.content);
            labelObj.transform.localPosition = Vector3.zero;
            labelObj.transform.localScale = Vector3.one;
            
            var slotUI = labelObj.GetComponent<PhotoSlotUI>();
            slotUI.Init();
            slotUI.SetData(data);
        }
    }
}
