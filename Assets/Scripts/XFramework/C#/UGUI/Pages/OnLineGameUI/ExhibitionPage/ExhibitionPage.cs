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
        TimeSpan timeSpan = OnLineGameManager.Instance.GetTimeUntilNextSunday(
            GameDataManager.Instance.PlayerData.GameDateTime);
        starDateTimeValue.SetVar("value",timeSpan.TotalDays);
        //TODO: 计算加成值
        //1.基础加成值的一半
        float exposure = GameDataManager.Instance.GetProperty(PropertyType.ExposureValue).Value / 2f;
        //2.计算服装加成
        float clothVal = 0;
        long equipClothingID = CharacterManager.Instance.GetCharacterBag(GameCostTools.MainCharacterID).ClothingID;
        ClothingData clothingData = CharacterManager.Instance.GetClothingDataByID(equipClothingID);
        if (clothingData != null)
        {
            clothVal = clothingData.ExposureValue;
        }

        if (equipClothingID == exhibitionInfoData.TargetClothingID)
        {
            clothVal *= exhibitionInfoData.AdditionValue;
        }

        float promotionVal = 0;
        //3.计算设备加成
        foreach (var promotionBag in OnLineGameManager.Instance.GetExhibitionPromotionBagList())
        {
            if (promotionBag.ExhibitionPromotionState == StateType.Unlock)
            {
                XFramework.ExhibitionPromotionData propertyData =
                    OnLineGameManager.Instance.GetExhibitionPromotionData(promotionBag.ExhibitionPromotionID);
                promotionVal += propertyData.ExposureValue;
            }
        }
        
        float total = exposure + clothVal + promotionVal;
        
        exposureVal.SetVar("value",$"{total}");
    }
}
