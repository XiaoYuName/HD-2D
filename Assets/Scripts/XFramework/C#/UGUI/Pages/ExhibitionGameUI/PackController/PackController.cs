using System;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.EventSystems;
using XFramework;

public partial class PackController : UIBase,IPointerClickHandler
{
    [LabelText("物品槽位")]
    public List<FlySlot> FlySlots = new List<FlySlot>();

    private int flySlotIndex = 0;
    private UIEffect uiEffect;
    
    public override void Init()
    {
        InitAutoBind();

        uiEffect = GetComponent<UIEffect>();
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        foreach (var flySlot in FlySlots)
        {
            flySlot.Init();
            flySlot.transform.localScale = UnityEngine.Vector3.zero;
        }
        flySlotIndex = 0;
        currentBagType = BagType.None;
        
    }

    public void SetEmpty()
    {
        SetSelected(false);
        SetBag(BagType.None);
        foreach (var flySlot in FlySlots)
        {
            flySlot.transform.DOScale(UnityEngine.Vector3.zero, 0.25f);
        }
        flySlotIndex = 0;
    }

    /// <summary>
    /// 获取一个空闲槽位
    /// </summary>
    /// <returns></returns>
    public FlySlot GetEmptyFlySlot()
    {
        if (flySlotIndex >= FlySlots.Count)
        {
            return null;
        }
        return FlySlots[flySlotIndex];
    }

    public void SetFlySlotData(FlyItemSlotData flyItemSlotData)
    {
        if (flySlotIndex >= FlySlots.Count) return;
        FlySlots[flySlotIndex].SetData(flyItemSlotData);
        FlySlots[flySlotIndex].transform.DOScale(UnityEngine.Vector3.one, 0.25f);
        flySlotIndex++;
    }

    public List<FactoryMerchandiseItemInfo> GetFlyItemSlotDataList()
    {
        List<FactoryMerchandiseItemInfo> result = new List<FactoryMerchandiseItemInfo>();
        for (int i = 0; i < flySlotIndex; i++)
        {
            result.Add(FlySlots[i].FlySlotData.ItemInfo);
        }
        return result;
    }


    private Sequence bagSequence;
    private BagType currentBagType;
    public void SetBag(BagType bagType)
    {
        bagSequence?.Kill();
        bagSequence = DOTween.Sequence();
        if (currentBagType == BagType.None)
        {
            currentBagType = bagType;
            switch (bagType)
            {
                case BagType.PaperBag:
                    bagSequence.Append(bagType01.DOFade(1f, 0.25f));
                    break;
                case BagType.PlasticBag:
                    bagSequence.Append(bagType02.DOFade(1f, 0.25f));
                    break;
            }
        }
        else
        {
            switch (currentBagType)
            {
                case BagType.PaperBag:
                    bagSequence.Append(bagType01.DOFade(0f, 0.25f));
                    break;
                case BagType.PlasticBag:
                    bagSequence.Append(bagType02.DOFade(0f, 0.25f));
                    break;
            }
            currentBagType = bagType;
            switch (currentBagType)
            {
                case BagType.PaperBag:
                    bagSequence.Append(bagType01.DOFade(1f, 0.25f));
                    break;
                case BagType.PlasticBag:
                    bagSequence.Append(bagType02.DOFade(1f, 0.25f));
                    break;
            }
        }
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        ExhibitionManager.Instance.Pack(this);
    }

    public void SetSelected(bool isSelected)
    {
        uiEffect.edgeMode = isSelected ? EdgeMode.Plain : EdgeMode.None;
    }
}
