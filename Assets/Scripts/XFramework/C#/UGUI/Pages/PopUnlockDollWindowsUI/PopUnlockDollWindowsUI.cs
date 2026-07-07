using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class PopUnlockDollWindowsUI : UIBase
{
    public Action OnClose;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(image,Close,"");
    }

    public ItemData ItemData { get; private set; }

    public void ShowItemInfo(long itemID,Action onClose)
    {
        ItemData  = InventoryManager.Instance.GetItemData(itemID);

        dollNameKey.SetText("InventoryItem",ItemData.NameKey);
        dollIcon.sprite = AssetsManager.Instance.LoadAssets<Sprite>(ItemData.IconPath);
        descString.SetText("InventoryItem",ItemData.DescKey);
        this.OnClose = onClose;
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        OnClose?.Invoke();
    }
}
