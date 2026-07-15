using System;
using UnityEngine;
using XFramework;

public partial class ExhibitionPage : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
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
        exhibitionName.SetText(exhibitionInfoData.ExhibitionInfoName);
        exhibitionBg.sprite =
            AssetsManager.Instance.LoadAssets<Sprite>(
                GamePathTools.CombinationExhibitionIconPath(exhibitionInfoData.IconName));
        exhibitionDesc.SetText(exhibitionInfoData.Desc);
        TimeSpan timeSpan = OnLineGameManager.Instance.GetTimeUntil(exhibitionInfoData.StartDateTime);
        starDateTimeValue.SetVar("value",timeSpan.TotalDays);
        //TODO: 计算加成值
        equipmentValue.SetVar("value",exhibitionInfoData.ExposureDeftual);
    }
}
