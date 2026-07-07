using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class FactoryMoldItemIcon : MonoBehaviour
{
    [SerializeField] Image frameImage, maskImage, paintingImage;

    public void Set(FactoryComposedItemInfo itemInfo)
    {
        long frameId = itemInfo.FrameItemId;
        long paintingId = itemInfo.PaintingItemId;

        MoldFrameConfig moldFrameConfig = AssetsManager.Instance.LoadAssets<MoldFrameConfig>(AssetKeys.MoldFrameConfigPath);
        PaintingConfig paintingConfig = AssetsManager.Instance.LoadAssets<PaintingConfig>(AssetKeys.PaintingConfigPath);

        frameImage.SetIcon(moldFrameConfig.GetFramePath(frameId));
        maskImage.SetIcon(moldFrameConfig.GetMaskPath(frameId));
        paintingImage.SetIcon(paintingConfig.GetComposedItemPath(paintingId, frameId));

        AssetsManager.Instance.FreeAsset(AssetKeys.MoldFrameConfigPath);
        AssetsManager.Instance.FreeAsset(AssetKeys.PaintingConfigPath);
    }
}
