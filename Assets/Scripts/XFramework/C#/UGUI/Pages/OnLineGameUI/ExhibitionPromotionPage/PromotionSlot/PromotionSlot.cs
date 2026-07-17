using System;
using XFramework;

public partial class PromotionSlot : UIBase
{
    public ExhibitionPromotionBag PromotionBag { get; private set; }
    public ExhibitionPromotionData ExhibitionPromotionData { get; private set; }


    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(ExhibitionPromotionBag promotionBag,Action<ExhibitionPromotionBag> OnSelected)
    {
        this.PromotionBag = promotionBag;
        ExhibitionPromotionData =
            OnLineGameManager.Instance.GetExhibitionPromotionData(promotionBag.ExhibitionPromotionID);
        nameString.SetText(ExhibitionPromotionData.PromotionName);
        valueString.SetVar("value",ExhibitionPromotionData.ExposureValue);
        if (promotionBag.ExhibitionPromotionState == StateType.Lock)
        {
            buyButton.SetLabel("UIText","Price");
            buyButton.SetLabelVal("value",ExhibitionPromotionData.Price.ToString());
        }
        else
        {
            buyButton.SetLabel("UIText","BuyComplete");
        }
        Bind(buyButton,()=> OnSelected(PromotionBag),"");
    }
}
