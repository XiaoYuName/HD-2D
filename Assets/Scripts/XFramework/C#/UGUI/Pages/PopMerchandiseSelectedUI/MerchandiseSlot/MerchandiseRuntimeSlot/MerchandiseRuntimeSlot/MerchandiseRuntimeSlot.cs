using UnityEngine;
using XFramework;

public partial class MerchandiseRuntimeSlot : UIBase
{
    public FactoryMerchandiseItemInfo itemInfo { get; private set; }
    private MoldFrameConfig ModeFarmeConfig;
    private PaintingConfig PaintingConfig;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(FactoryMerchandiseItemInfo itemInfo)
    {
        this.itemInfo = itemInfo;
        itemCountVal.text = $"X{itemInfo.Count}";
        priceVal.SetVar("value",itemInfo.GetValue().ToString());
       
        ModeFarmeConfig = AssetsManager.Instance.LoadAssets<MoldFrameConfig>(AssetKeys.MoldFrameConfigPath);
        PaintingConfig = AssetsManager.Instance.LoadAssets<PaintingConfig>(AssetKeys.PaintingConfigPath);
        mask.sprite = AssetsManager.Instance.LoadAssets<Sprite>(ModeFarmeConfig.GetMaskPath(itemInfo.FrameItemId));
        farmeImg.sprite = AssetsManager.Instance.LoadAssets<Sprite>(ModeFarmeConfig.GetFramePath(itemInfo.FrameItemId));
        itemIcon.sprite = AssetsManager.Instance.LoadAssets<Sprite>(PaintingConfig.GetComposedItemPath(itemInfo.PaintingItemId
        ,itemInfo.FrameItemId));
        
    }

    public void Release()
    {
        AssetsManager.Instance.FreeAsset(ModeFarmeConfig.GetMaskPath(itemInfo.FrameItemId));
        AssetsManager.Instance.FreeAsset(ModeFarmeConfig.GetFramePath(itemInfo.FrameItemId));
        AssetsManager.Instance.FreeAsset(PaintingConfig.GetComposedItemPath(itemInfo.PaintingItemId
            ,itemInfo.FrameItemId));
        
        AssetsManager.Instance.FreeAsset(AssetKeys.MoldFrameConfigPath);
        AssetsManager.Instance.FreeAsset(AssetKeys.PaintingConfigPath);
    }
}
