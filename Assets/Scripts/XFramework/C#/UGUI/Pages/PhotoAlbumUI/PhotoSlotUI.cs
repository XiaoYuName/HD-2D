using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class PhotoSlotUI : UIBase
{
    private RawImage _rawImage;
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _rawImage = Get<RawImage>("Sprite_CG");
    }

    public void SetData(ActionCGData cgData)
    {
        _rawImage.texture = AssetsManager.Instance.LoadAssets<Texture2D>(cgData.minSpritePath);
        
    }
}
