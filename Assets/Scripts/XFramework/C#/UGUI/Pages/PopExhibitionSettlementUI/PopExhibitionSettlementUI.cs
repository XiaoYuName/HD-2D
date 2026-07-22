using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using XFramework;

public partial class PopExhibitionSettlementUI : UIBase
{
    private List<SettlementSlot> _settlementSlots = new List<SettlementSlot>();
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
        PlayerInputManager.Instance.OnClick += Close;
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        PlayerInputManager.Instance.OnClick -= Close;
        base.Close();
        foreach (var settlementSlot in _settlementSlots)
        {
            settlementSlot.Release();
            AssetsManager.Instance.FreeGameObject(settlementSlot.gameObject);
        }
        _settlementSlots.Clear();
        ExhibitionManager.Instance.QuitExhibition();
    }

    private Sequence _sequence;
    private float minDuration = 0.5f;
    private float maxDuration = 3f;
    private int maxDurationCount = 10000;

    public void SetData(int coinNumber,int fenNumber,int GoodwillNumber,List<FactoryMerchandiseItemInfo> itemInfos)
    {
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

        if (equipClothingID == ExhibitionManager.Instance.ExhibitionInfoData.TargetClothingID)
        {
            clothVal *= ExhibitionManager.Instance.ExhibitionInfoData.AdditionValue;
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


        int totalFenNumber = Mathf.RoundToInt(fenNumber * total);
        
        int coinCount = 0;
        int fenCount = 0;
        int GoodwillCount = 0;
        
        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _sequence.Append(DOTween.To(() => coinCount, x =>
        {
            coinCount = x;
            coinTotalValueTex.text = $"+{coinCount}";
        }, coinNumber,CalculateDuration(coinNumber)));
        _sequence.Join(DOTween.To(() => fenCount, x =>
        {
            fenCount = x;
            fenTotalValue.text  = $"+{fenCount}";
        }, totalFenNumber,CalculateDuration(totalFenNumber)));
        _sequence.Join(DOTween.To(() => GoodwillCount, x =>
        {
            GoodwillCount = x;
            goodwillTotalValue.text = $"+{GoodwillCount}";
        }, GoodwillNumber,CalculateDuration(GoodwillNumber)));
        
        GameDataManager.Instance.AddProperty(PropertyType.Coin,coinNumber);
        GameDataManager.Instance.AddProperty(PropertyType.FenCount,fenNumber);
        CharacterManager.Instance.AddCharacterFavorability(GameCostTools.MainCharacterID,GoodwillNumber);
        
        CreatSettlementSlot(itemInfos);
    }

    private void CreatSettlementSlot(List<FactoryMerchandiseItemInfo> itemInfos)
    {
        int sellNumber = 0;
        int totalNumber = 0;
        foreach (var itemInfo in itemInfos)
        {
           var obj =  AssetsManager.Instance.Instantiate(AssetKeys.SettlementSlotPath);
           obj.transform.SetParent(scrollView.content);
           obj.transform.localScale = Vector3.one;
           var slot = obj.GetComponent<SettlementSlot>();
           sellNumber += itemInfo.Count;
           slot.Init();
           slot.SetData(itemInfo);
           _settlementSlots.Add(slot);
        }

        foreach (var itemInfo in ExhibitionManager.Instance.OnSelectedFactory)
        {
            totalNumber += itemInfo.Count;
        }
        
        settlementProcessVal.SetVar("value",sellNumber);
        settlementProcessVal.SetVar("max",totalNumber);
        completeValTex.SetVar("value",itemInfos.Count);
        settlementValTex.SetVar("value",totalNumber);
        limitButton.gameObject.SetActive(sellNumber >= totalNumber);
        
        foreach (var itemInfo in itemInfos)
        {
            InventoryManager.Instance.ConsumeItem(itemInfo.ID, itemInfo.Count);
        }
    }


    private float CalculateDuration(int number)
    {
        float progress = Mathf.InverseLerp(
            0,
            maxDurationCount,
            Mathf.Abs((float)number)
        );

        return Mathf.Lerp(minDuration, maxDuration, progress);
    }
}
