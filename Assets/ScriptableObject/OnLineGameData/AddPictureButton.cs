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

    public override void Release()
    {
        // 必须置空:Release 现在会跟着 Close/OnDestroy 走,不置空的话重复释放会把引用计数打成负数
        if (ItemData != null)
        {
            rawImage.texture = null;
            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationItemIconPath(ItemData.IconName));
            ItemData = null;
        }

        base.Release();
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
                    AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationItemIconPath(ItemData.IconName)).texture;
                rawImage.gameObject.SetActive(true);
            }
        }
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
}
