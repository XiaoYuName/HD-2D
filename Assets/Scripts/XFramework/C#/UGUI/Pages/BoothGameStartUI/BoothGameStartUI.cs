using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class BoothGameStartUI : UIBase
{
    private ExhibitionInfoData exhibitionInfo;
    
    private List<MerchandiseSelectedSlot> exhibitionSlots = new List<MerchandiseSelectedSlot>();
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(optionClothingButton, () =>
        {
            UISystem.Instance.OpenUI<PopClothingSelectedUI>("PopClothingSelectedUI");
        },"");
        Bind(mask,OpenAddPopMerchandiseSelectedUI,"");
        Bind(addFactoryButton,OpenAddPopMerchandiseSelectedUI,"");
        Bind(autoAddButton, () =>
        {
            ExhibitionManager.Instance.AutoAddFactoryList();
        },"");
        Bind(btnStart,EnterExhibitionGame,"");
        Bind(btnSkip,SkipExhibition,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataChange(PlayerDataChange);
        CharacterManager.Instance.RegisterCharacterBagChange(GameCostTools.MainCharacterID,CharacterBagChange);
        ShowingExhibitionInfo();
        ExhibitionManager.Instance.RegisterSelectedFactoryUpdate(SelectedFactoryChange);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerDataChange);
        CharacterManager.Instance.UnregisterCharacterBagChange(GameCostTools.MainCharacterID,CharacterBagChange);
        ExhibitionManager.Instance.UnRegisterSelectedFactoryUpdate(SelectedFactoryChange);
        // 这里不用再显式调 Release():base.Close() 已经会走到重写后的 Release
    }

    public override void Release()
    {
        if (exhibitionInfo != null)
        {
            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationExhibitionIconPath(exhibitionInfo.IconName));
            exhibitionInfo = null;
        }
        foreach (var slot in exhibitionSlots)
        {
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        exhibitionSlots.Clear();
        base.Release();
    }

    private void PlayerDataChange(PlayerData playerData)
    {
        fenCount.text = $"{playerData.GetProperty(PropertyType.FenCount)}";
    }

    private void CharacterBagChange(CharacterBag characterBag)
    {
        CharacterData characterData = CharacterManager.Instance.GetCharacterDataByID(characterBag.CharacterID);
        if (characterData != null)
        {
            clothingNameTip.SetVar("CharacterName",LanguageManager.Instance.GetLocalizedString(characterData.Name.Table,characterData.Name.Value));
            ClothingData clothingData = CharacterManager.Instance.GetClothingDataByID(characterBag.ClothingID);
            if (clothingData == null)
            {
                clothingNameTip.SetVar("ClothingName",LanguageManager.Instance.GetLocalizedString("UIText","None"));
            }
            else
            {
                clothingNameTip.SetVar("ClothingName",LanguageManager.Instance.GetLocalizedString(clothingData.ClothingName.Table,clothingData.ClothingName.Value));
            }
        }
    }

    private void SelectedFactoryChange(List<FactoryMerchandiseItemInfo> item)
    {
        if(item is not { Count: > 0 })
        {
            mask.gameObject.SetActive(true);
            merchandiseView.gameObject.SetActive(false);
            foreach (var slot in exhibitionSlots)
            {
                slot.Release();
                AssetsManager.Instance.FreeGameObject(slot.gameObject);
            }
            exhibitionSlots.Clear();

            return;
        }
        mask.gameObject.SetActive(false);
        merchandiseView.gameObject.SetActive(true);
        foreach (var slot in exhibitionSlots)
        {
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        exhibitionSlots.Clear();

        int totalCount = 0;
        foreach (var data in item)
        {
           var obj =  AssetsManager.Instance.Instantiate(AssetKeys.MerchandiseSelectedSlotPath);
           obj.transform.SetParent(itemScrollRect.content);
           obj.transform.localScale = Vector3.one;
           
           var slot = obj.GetComponent<MerchandiseSelectedSlot>();
           slot.Init();
           slot.SetData(data,OnReleased);
           totalCount += data.Count;
           exhibitionSlots.Add(slot);
        }
        
        totalValTex.SetVar("value",totalCount);
    }

    private void OnReleased(MerchandiseSelectedSlot slot)
    {
        ExhibitionManager.Instance.SubFactoryItem(slot.CurrentData);
    }

    private void OpenAddPopMerchandiseSelectedUI()
    {
        UISystem.Instance.OpenUI<PopMerchandiseSelectedUI>("PopMerchandiseSelectedUI");
    }

    private void ShowingExhibitionInfo()
    {
        exhibitionInfo = ExhibitionManager.Instance.ExhibitionInfoData;
        if (exhibitionInfo == null) return;
        icon.sprite =
            AssetsManager.Instance.LoadAssets<Sprite>(
                GamePathTools.CombinationExhibitionIconPath(exhibitionInfo.IconName));
        desc.SetText(exhibitionInfo.Desc);
    }

    private void EnterExhibitionGame()
    {
        if (ExhibitionManager.Instance.OnSelectedFactory.Count <= 0)
        {
            UIUtility.ShowPopWindow("NotFactoryMerchandiseItemInfo");
            return;
        }

        ExhibitionManager.Instance.EnterExhibition();
        Close();
    }

    private void SkipExhibition()
    {
        Close();
        ExhibitionManager.Instance.QuitExhibition();
    }
}
