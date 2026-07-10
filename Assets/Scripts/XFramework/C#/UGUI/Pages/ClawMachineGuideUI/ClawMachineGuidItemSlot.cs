using System;
using UnityEditor.Localization.Plugins.XLIFF.V12;
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
    public GuideBag GuideBag { get; private set; }
    
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
            ItemData  = InventoryManager.Instance.GetItemData(data.ID);
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

    public void Release()
    {
        if (ItemData != null)
        {
            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationSceneImagePath(ItemData.IconName));
        }

        if (DollCatalogData != null)
        {
           
            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationDollImagePath(DollCatalogData.UlockImageName));
            DollCatalogData = null;
        }
        
        OnClick  = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke(this);
    }
}
