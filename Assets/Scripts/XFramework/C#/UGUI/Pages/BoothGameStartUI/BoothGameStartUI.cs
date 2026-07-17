using UnityEngine;
using XFramework;

public partial class BoothGameStartUI : UIBase
{
    private ExhibitionInfoData exhibitionInfo;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(optionClothingButton, () =>
        {
            UISystem.Instance.OpenUI<PopClothingSelectedUI>("PopClothingSelectedUI");
        },"");
        Bind(mask,OpenAddPopMerchandiseSelectedUI,"");
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
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerDataChange);
        CharacterManager.Instance.UnregisterCharacterBagChange(GameCostTools.MainCharacterID,CharacterBagChange);
        Release();
    }

    private void Release()
    {
        if (exhibitionInfo != null)
        {
            AssetsManager.Instance.FreeAsset(GamePathTools.CombinationExhibitionIconPath(exhibitionInfo.IconName));
            exhibitionInfo = null;
        }
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
    

}
