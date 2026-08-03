using UnityEngine;
using XFramework;

public partial class UpperSlot : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(ClothingAccessoriesData accessoriesData)
    {
        icon.sprite =
            LoadAsset<Sprite>(GamePathTools.CombinationAccessoriesIconPath(accessoriesData.AccessoriesIconName));
    }

    public void SetSelected(bool isSelected)
    {
        selected.gameObject.SetActive(isSelected);
    }
}
