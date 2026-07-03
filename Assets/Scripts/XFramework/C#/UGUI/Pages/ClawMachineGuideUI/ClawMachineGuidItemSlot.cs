using System;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class ClawMachineGuidItemSlot : UIBase
{
    private Image image;
    private Image ulockImage;
    private LocalizeStringEvent localizeStringEvent;

    public DollCatalogData DollCatalogData { get; private set; }
    public GuideBag GuideBag { get; private set; }

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
        if (data != null)
        {
            image.sprite =
                AssetsManager.Instance.LoadAssets<Sprite>(
                    GuideManager.Instance.CombinationDollImagePath(data.ImageName));

            ulockImage.sprite =
                AssetsManager.Instance.LoadAssets<Sprite>(
                    GuideManager.Instance.CombinationDollImagePath(data.UlockImageName));
            
            
            image.gameObject.SetActive(false);
            ulockImage.gameObject.SetActive(true);
        }
    }

    public void SetIndexLabel(int index)
    {
        localizeStringEvent.SetVar("Index",index.ToString());
    }

    public void UpdateData(GuideBag guidBag)
    {
        if (guidBag != null)
        {
            switch (guidBag.StateType)
            {
                case StateType.None:
                    break;
                case StateType.Lock:
                    image.gameObject.SetActive(true);
                    ulockImage.gameObject.SetActive(false);
                    break;
                case StateType.Unlock:
                    image.gameObject.SetActive(false);
                    ulockImage.gameObject.SetActive(true);
                    break;
            }
        }
    }

    public void Release()
    {
        if (DollCatalogData != null)
        {
            AssetsManager.Instance.FreeAsset(GuideManager.Instance.CombinationDollImagePath(DollCatalogData.ImageName));
            AssetsManager.Instance.FreeAsset(GuideManager.Instance.CombinationDollImagePath(DollCatalogData.UlockImageName));
            DollCatalogData = null;
        }
    }

}
