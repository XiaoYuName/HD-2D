using System;
using UnityEngine;
using XFramework;

public partial class ExhibitionPage : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        rankOne.Init();
        rankTow.Init();
        rankThree.Init();
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
        exposureVal.SetVar("value",exhibitionInfoData.ExposureDeftual);
        int rank = OnLineGameManager.Instance.GetExhibitionRanking(GameDataManager.Instance
            .GetProperty(PropertyType.Popularity).Value);
        
        totalVal.SetVar("Rank",rank);
        totalVal.SetVar("FenCount",exhibitionInfoData.ExposureDeftual);

        if (rank != 1)
        {
            var oneData = OnLineGameManager.Instance.GetRankPopularityData(1);
            if (oneData != null)
            {
                rankOne.SetLabel(oneData.Name.Table, oneData.Name.Value,oneData.Value.ToString());
            }
        }
        else
        {
            rankOne.SetLabel("CharacterNames", "Character_NPC_02",exhibitionInfoData.ExposureDeftual.ToString());
        }

        
        if (rank != 2)
        {
            var oneData = OnLineGameManager.Instance.GetRankPopularityData(2);
            if (oneData != null)
            {
                rankTow.SetLabel(oneData.Name.Table, oneData.Name.Value,oneData.Value.ToString());
            }
        }
        else
        {
            rankTow.SetLabel("CharacterNames", "Character_NPC_02",exhibitionInfoData.ExposureDeftual.ToString());
        }
        
        if (rank != 3)
        {
            var oneData = OnLineGameManager.Instance.GetRankPopularityData(3);
            if (oneData != null)
            {
                rankThree.SetLabel(oneData.Name.Table, oneData.Name.Value,oneData.Value.ToString());
            }
        }
        else
        {
            rankThree.SetLabel("CharacterNames", "Character_NPC_02",exhibitionInfoData.ExposureDeftual.ToString());
        }


    }
}
