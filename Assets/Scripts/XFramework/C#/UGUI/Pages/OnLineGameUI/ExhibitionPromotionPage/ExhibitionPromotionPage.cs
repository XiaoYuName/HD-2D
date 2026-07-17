using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class ExhibitionPromotionPage : UIBase
{
    private List<PromotionSlot>  promotionSlots = new List<PromotionSlot>();
    
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
        OnLineGameManager.Instance.RegisterOnExhibitionPromotionBagUpdate(GeneratePromotionSlots);
        GameDataManager.Instance.RegisterPlayerDataChange(PlayerDataChange);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        foreach (var slot in promotionSlots)
        {
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        
        OnLineGameManager.Instance.UnRegisterOnExhibitionPromotionBagUpdate(GeneratePromotionSlots);
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerDataChange);
    }

    public void GeneratePromotionSlots(List<ExhibitionPromotionBag> promotions)
    {
        foreach (var slot in promotionSlots)
        {
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        promotionSlots.Clear();

        int totalVal = 0;
        foreach (var promotionBag in promotions)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.PromotionSlotPath);
            obj.transform.SetParent(scrollView.content);
            obj.transform.localScale = Vector3.one;
            
            var slot = obj.GetComponent<PromotionSlot>();
            slot.Init();
            if (promotionBag.ExhibitionPromotionState == StateType.Unlock)
            {
                var data = OnLineGameManager.Instance.GetExhibitionPromotionData(promotionBag.ExhibitionPromotionID);
                totalVal += data.ExposureValue;
            }
            slot.SetData(promotionBag,BuyPromotionSlot);
            promotionSlots.Add(slot);
        }
        promotionValue.SetVar("value",totalVal);
        
    }

    public void PlayerDataChange(PlayerData playerData)
    {
        goldVal.SetVar("value",playerData.GetProperty(PropertyType.Coin));
    }

    public void BuyPromotionSlot(ExhibitionPromotionBag promotionBag)
    {
        ExhibitionPromotionData data = OnLineGameManager.Instance.GetExhibitionPromotionData(promotionBag.ExhibitionPromotionID);
        if (data == null)
        {
            return;
        }

        if (GameDataManager.Instance.GetProperty(PropertyType.Coin).Value >= data.Price)
        {
            GameDataManager.Instance.RemoveProperty(PropertyType.Coin,data.Price);
           
            string coinTip = $"{LanguageManager.Instance.GetLocalizedString(GameDataManager.Instance.GetPropertyData(PropertyType.Coin).Name)} - {data.Price}";
            
            ExhibitionPromotionData exhibitionPromotionData = OnLineGameManager.Instance.GetExhibitionPromotionData(promotionBag.ExhibitionPromotionID);

            string exhibitionTip =
                $"{LanguageManager.Instance.GetLocalizedString(GameDataManager.Instance.GetPropertyData(PropertyType.ExposureValue).Name)} " +
                $"+ {exhibitionPromotionData.ExposureValue}";
            
            
            OnLineGameManager.Instance.BuyExhibitionPromotionBag(promotionBag.ExhibitionPromotionID);
            UIUtility.PopRewardProperty(new List<string>(){coinTip,exhibitionTip}, () =>
            {
                GameDataManager.Instance.Sleep();
            });
           
        }
        else
        {
            UIUtility.ShowPopDialogue("BuyError_Gold");
        }



    }
}

