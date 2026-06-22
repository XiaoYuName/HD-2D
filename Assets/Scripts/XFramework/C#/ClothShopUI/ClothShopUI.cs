using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 布料商店
/// </summary>
public class ClothShopUI : UIBase
{
    private LocalizeStringEvent currentGoldStringEvent;
    private Button CloseButton;

    private ScrollRect ShopItemScrollRect;
    
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        Bind(CloseButton,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.BindUserChange(UpdateUserUI);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnBindUserChange(UpdateUserUI);
    }


    private void UpdateUserUI(User user)
    {
        currentGoldStringEvent.SetVar("value",user.GoldNumber);
    }
}

