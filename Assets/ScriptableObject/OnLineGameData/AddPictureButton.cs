using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public class AddPictureButton : UIBase,IPointerClickHandler
{
    public UnityEvent OnClick;

    public ItemData ItemData { get; private set; }

    private RawImage rawImage;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        rawImage = Get<RawImage>("RawImage");
    }

    public void Release()
    {
        if (ItemData != null)
        {
           AssetsManager.Instance.FreeAsset(GamePathTools.CombinationItemIconPath(ItemData.IconName));
        }
    }

    public void SetData(ItemInfo itemInfo)
    {
        if (itemInfo == null)
        {
            rawImage.gameObject.SetActive(false);
        }
        else
        {
            ItemData = itemInfo.GetItemData();
            if (ItemData != null)
            {
                rawImage.texture =
                    AssetsManager.Instance.LoadAssets<Texture2D>(GamePathTools.CombinationItemIconPath(ItemData.IconName));
                rawImage.gameObject.SetActive(true);
            }
        }
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
}
