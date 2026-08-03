using UnityEngine;
using UnityEngine.UI;
using XFramework;

public partial class EquipClothingSlot : UIBase
{
    public ClothingAccessoriesData Data { get; private set; }
    public RectTransform Rect { get; private set; }

    /// <summary>
    /// 图片实际内容(不含透明留白)的中心,自身本地坐标。
    /// 素材四周常有大片留白,Rect 中心并不是玩家看到的图形中心。
    /// </summary>
    public Vector2 ContentLocalCenter { get; private set; }

    private Image iconImage;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        iconImage = GetComponent<Image>();
        Rect = GetComponent<RectTransform>();
    }

    public void SetData(ClothingAccessoriesData accessoriesData)
    {
        Data = accessoriesData;
        iconImage.sprite = LoadAsset<Sprite>(GamePathTools.CombinationAccessoriesIconPath(accessoriesData.AccessoriesMaxIconName));
        iconImage.SetNativeSize();
        ContentLocalCenter = UISpriteShapeUtils.TryGetContentBounds(iconImage, Rect, Rect, out var contentBounds)
            ? contentBounds.center
            : Rect.rect.center;
    }
}
