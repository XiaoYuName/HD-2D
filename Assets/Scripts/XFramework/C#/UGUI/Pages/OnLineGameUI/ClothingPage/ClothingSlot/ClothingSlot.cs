using System;
using UnityEngine;
using XFramework;

public partial class ClothingSlot : UIBase
{
    public ClothingData ClothingData { get; private set; }

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(selectedButton, () =>
        {
            OnClick?.Invoke(this);
        },"");
    }

    private Action<ClothingSlot> OnClick;

    public void SetData(ClothingData data,Action<ClothingSlot> onClick = null)
    {
        ClothingData = data;
        icon.sprite =
            AssetsManager.Instance.LoadAssets<Sprite>(
                GamePathTools.CombinationClothingImagePath(data.ClothingIconName));
        nameString.SetText(data.ClothingName);
        selectedButton.SetSelected(false);
        OnClick = onClick;
    }

    public void SetSelected(bool selected)
    {
        selectedButton.SetSelected(selected);
        if (selected)
        {
            selectedButton.SetLabel(new LocalSelectedData()
            {
                Table = "UIText",
                Value = "Selected"
            });
        }
        else
        {
            selectedButton.SetLabel(new LocalSelectedData()
            {
                Table = "UIText",
                Value = "NodeSelected"
            });
        }
    }
    
}
