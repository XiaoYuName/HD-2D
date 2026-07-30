using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class ClawMachineGuidItemSlot : UIBase,IPointerClickHandler
{
    private Image image;
    private Image ulockImage;
    private LocalizeStringEvent localizeStringEvent;

    public DollCatalogData DollCatalogData { get; private set; }
    public ItemData ItemData { get; private set; }
    
    public event Action<ClawMachineGuidItemSlot> OnClick;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        image = Get<Image>("icon");
        ulockImage = Get<Image>("ulockImage");
        localizeStringEvent = Get<LocalizeStringEvent>("Text");

    }

    public void SetData(DollCatalogData data)
    {
        DollCatalogData = data;
        if (data != null)
        {
            ItemData  = InventoryManager.Instance.GetItemData(data.ItemID);
            if (ItemData != null)
            {
                image.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationItemIconPath(ItemData.IconName));
            }
            ulockImage.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationDollImagePath(data.UlockImageName));
            
            
            image.gameObject.SetActive(false);
            ulockImage.gameObject.SetActive(true);
        }
    }

    public void SetIndexLabel(int index)
    {
        localizeStringEvent.SetVar("Index",index.ToString());
    }

    public void UpdateData(ItemInfo itemInfo)
    {
        if (itemInfo != null)
        {
            if (!InventoryManager.Instance.HasItemUnlock(itemInfo.ID))
            {
                image.gameObject.SetActive(false);
                ulockImage.gameObject.SetActive(true);
            }
            else
            {
                image.gameObject.SetActive(true);
                ulockImage.gameObject.SetActive(false);
            }
            
            
        }
    }

    public override void Release()
    {
        // 必须置空:Release 现在会跟着 Close/OnDestroy 走,不置空的话重复释放会把引用计数打成负数
        if (ItemData != null)
        {
            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationSceneImagePath(ItemData.IconName));
            ItemData = null;
        }

        if (DollCatalogData != null)
        {

            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationDollImagePath(DollCatalogData.UlockImageName));
            DollCatalogData = null;
        }

        OnClick  = null;
        base.Release();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke(this);
    }
}
