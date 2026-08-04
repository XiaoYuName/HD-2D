using System;
using UnityEngine;
using XFramework;

public partial class ClothingSlot : UIBase
{
    /// <summary>未解锁时图标压暗，和已解锁的服装区分开</summary>
    private static readonly Color LockedIconColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    public ClothingData ClothingData { get; private set; }

    /// <summary>服装是否已解锁，来自角色背包里的 ClothingBag，不是配置表</summary>
    public bool IsUnlock { get; private set; }

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(selectedButton, () =>
        {
            // 没解锁的服装不响应点击，按钮本身也是禁用的，这里只是双保险
            if (!IsUnlock) return;
            OnClick?.Invoke(this);
        },"");
    }

    private Action<ClothingSlot> OnClick;

    public override void Release()
    {
        if (ClothingData != null)
        {
            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationClothingImagePath(ClothingData.ClothingIconName));
            ClothingData = null;
        }

        IsUnlock = false;
        base.Release();
    }

    public void SetData(ClothingData data,bool isUnlock,Action<ClothingSlot> onClick = null)
    {
        ClothingData = data;
        IsUnlock = isUnlock;
        icon.sprite =
            AssetsManager.Instance.LoadAssets<Sprite>(
                GamePathTools.CombinationClothingImagePath(data.ClothingIconName));
        icon.color = isUnlock ? Color.white : LockedIconColor;
        nameString.SetText(data.ClothingName);
        selectedButton.interactable = isUnlock;
        SetSelected(false);
        OnClick = onClick;
    }

    public void SetSelected(bool selected)
    {
        // 未解锁的服装既不能选中，也不显示"选择/已选择"，统一显示为已锁定
        if (!IsUnlock)
        {
            selectedButton.SetSelected(false);
            selectedButton.SetLabel(new LocalSelectedData()
            {
                Table = "UIText",
                Value = "NoLock"
            });
            return;
        }

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
