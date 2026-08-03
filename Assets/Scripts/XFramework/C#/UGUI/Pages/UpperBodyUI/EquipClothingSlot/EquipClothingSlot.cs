using UnityEngine;
using UnityEngine.UI;
using XFramework;

public partial class EquipClothingSlot : UIBase
{
    private Image iconImage;
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        iconImage = GetComponent<Image>();
    }

    public void SetData(ClothingAccessoriesData accessoriesData)
    {
        iconImage.sprite = LoadAsset<Sprite>(GamePathTools.CombinationAccessoriesIconPath(accessoriesData.AccessoriesMaxIconName));
        iconImage.SetNativeSize();
    }
}
