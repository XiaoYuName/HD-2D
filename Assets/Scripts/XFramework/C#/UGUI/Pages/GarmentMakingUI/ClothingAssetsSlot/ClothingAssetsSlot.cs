using System;
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using XFramework;

public partial class ClothingAssetsSlot : UIBase,IPointerClickHandler
{
    public UnityEvent<ClothingAssetsSlot> OnSelect;

    public ClothingBag CurrentBag { get; private set; }
    public ClothingData CurrentData { get; private set; }

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(ClothingBag clothingBag)
    {
        try
        {
            CurrentBag = clothingBag;
            var data =  LubanManager.Instance.TbClothingData.Get(clothingBag.clothingID);
            CurrentData = data;
            clothingName.SetText(data.ClothingName);
            clothingImg.sprite = LoadAsset<Sprite>(GamePathTools.CombinationClothingImagePath(data.ClothingIconName));
            clothingImg.SetIcon(GamePathTools.CombinationClothingImagePath(data.ClothingIconName));
        }
        catch (Exception e)
        {
            CurrentData = null;
            Debug.Log("没有找到对应的服装 ：" + clothingBag.clothingID +"   "+ e.Message);
        }
    }

    public void SetSelected(bool selected)
    {
        farme.edgeMode = selected ? EdgeMode.Plain : EdgeMode.None;
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        OnSelect?.Invoke(this);
    }
}
