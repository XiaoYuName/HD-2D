using System;
using UnityEngine;
using XFramework;

public partial class ExhibitionPage : UIBase
{
    public override void Init()
    {
        InitAutoBind();
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        ShowingExhibitionPage(OnLineGameManager.Instance.GetRecentExhibitionInfo());
        
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        
    }


    private void ShowingExhibitionPage(ExhibitionInfoData exhibitionInfoData)
    {
        if (exhibitionInfoData == null) return;
        exhibitionName.SetText(exhibitionInfoData.ExhibitionInfoName);
        exhibitionBg.sprite =
            AssetsManager.Instance.LoadAssets<Sprite>(
                GamePathTools.CombinationExhibitionIconPath(exhibitionInfoData.IconName));
        exhibitionDesc.SetText(exhibitionInfoData.Desc);
        TimeSpan timeSpan = OnLineGameManager.Instance.GetTimeUntil(exhibitionInfoData.StartDateTime);
        starDateTimeValue.SetVar("value",timeSpan.TotalDays);
        //TODO: 计算加成值
        exposureVal.SetVar("value","0");
    }
}
