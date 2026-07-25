using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using XFramework;

public partial class GarmentMakingUI : UIBase
{
    private List<ClothingAssetsSlot>  _clothingBags = new List<ClothingAssetsSlot>();

    private enum OptionType
    {
        Node,
        Clothing,
        Info,
        GameInfo,
    }

    private OptionType optionType;

    public override void Init()
    {
        InitAutoBind();
        clothingFittingUI.Init();
        clothingItemInfo.Init();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        commonTopUI.Init();
        commonTopUI.SetTitle(uiPageData.PageID,"Title");
        commonTopUI.SetClose(Close);
        
        optionType = OptionType.Node;
        Bind(starButton, StartProductionClothing,"");
        Bind(quitButton, () => { Option(OptionType.Clothing);},"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        commonTopUI.Open();
        starButton.interactable = false;
        CharacterManager.Instance.RegisterCharacterBagChange(GameCostTools.MainCharacterID,SelectedCharacterBagChange);
        Option(OptionType.Clothing);
    }
    
    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        commonTopUI.Close();
        CharacterManager.Instance.UnregisterCharacterBagChange(GameCostTools.MainCharacterID,SelectedCharacterBagChange);
        ClearClothingAssetSlots();
    }
    
    private void SelectedCharacterBagChange(CharacterBag characterBag)
    {
       var selectedClothingID = selectedClothingAssetsSlot != null
           ? selectedClothingAssetsSlot.CurrentBag.clothingID
           : 0;

       ClearClothingAssetSlots();
       selectedClothingAssetsSlot = null;
       starButton.interactable = false;

       foreach (var clothingBag in characterBag.ClothingBags)
       {
           //已解锁的不再展示
           if(clothingBag.isUnlock)continue;
           
           var obj = AssetsManager.Instance.Instantiate(AssetKeys.ClothingAssetsSlotPath);
           obj.transform.SetParent(scrollView.content);
           obj.transform.localScale = Vector3.one;

           var cloth = obj.GetComponent<ClothingAssetsSlot>();
           cloth.Init();
           cloth.SetData(clothingBag);
           cloth.OnSelect.RemoveAllListeners();
           cloth.OnSelect.AddListener(OnSelectedClothingAssetsSlot);
           _clothingBags.Add(cloth);
       }

       if (selectedClothingID > 0)
       {
           selectedClothingAssetsSlot = _clothingBags.Find(slot => slot.CurrentBag.clothingID == selectedClothingID);
           if (selectedClothingAssetsSlot != null)
           {
               selectedClothingAssetsSlot.SetSelected(true);
               starButton.interactable = true;
           }
       }

       if (optionType == OptionType.Info && selectedClothingAssetsSlot != null)
       {
           clothingFittingUI.SetDataList(_clothingBags.Select(t => t.CurrentBag).ToList(),
               _clothingBags.FindIndex(t => t == selectedClothingAssetsSlot));
       }
    }

    private ClothingAssetsSlot selectedClothingAssetsSlot;
    private Sequence FadeSequence;
    private void OnSelectedClothingAssetsSlot(ClothingAssetsSlot slot)
    {
        if (selectedClothingAssetsSlot == slot)
        {
            selectedClothingAssetsSlot.SetSelected(false);
            selectedClothingAssetsSlot = null;
            starButton.interactable = false;
            FadeSequence?.Kill();
            FadeSequence = DOTween.Sequence();
            FadeSequence.Append(nodeFace.DOFade(1, 0.3f));
            FadeSequence.Append(characterNormal.DOFade(0, 0.3f));
            return;
        }

        foreach (var assetsSlot in _clothingBags)
        {
            if (assetsSlot == slot)
            {
                selectedClothingAssetsSlot = assetsSlot;
                selectedClothingAssetsSlot.SetSelected(true);
                starButton.interactable = true;
                FadeSequence.Append(nodeFace.DOFade(0, 0.3f));
                FadeSequence.Append(characterNormal.DOFade(1, 0.3f));
            }
            else
            {
                assetsSlot.SetSelected(false);
            }
        }
    }

    #region Option
    
    private void StartProductionClothing()
    {
        if (selectedClothingAssetsSlot == null) return;
        Option(OptionType.Info);
        clothingFittingUI.SetDataList(_clothingBags.Select(t=> t.CurrentBag).ToList(),
            _clothingBags.FindIndex(t=>t == selectedClothingAssetsSlot));
    }

    private void Option(OptionType type)
    {
        if (type == optionType) return;
        
        optionType = type;
        quitButton.gameObject.SetActive(optionType != OptionType.Clothing && optionType !=  OptionType.Node);
        if (type == OptionType.Clothing)
        {
            viewPanel.gameObject.SetActive(true);
            clothingFittingUI.Close();
            clothingItemInfo.Close();
            
        }
        else if (type == OptionType.Info)
        {
            viewPanel.gameObject.SetActive(false);
            clothingItemInfo.Close();
            clothingFittingUI.Open();
           
        }else if (type == OptionType.GameInfo)
        {
            viewPanel.gameObject.SetActive(false);
            clothingFittingUI.Close();
            clothingItemInfo.Open();
        }

    }

    public void StarMinGameInfoClothing(ClothingBag clothingBag
        ,ClothingAccessoriesData accessoriesData)
    {
        Option(OptionType.GameInfo);
        clothingItemInfo.SetDataList(clothingBag,accessoriesData);
    }

    public void OptionReset()
    {
        StartProductionClothing();
    }

    public void RefreshClothingFittingData(long characterID, long clothingID)
    {
        var characterBag = CharacterManager.Instance.GetCharacterBag(characterID);
        var clothingBag = characterBag?.ClothingBags.Find(temp => temp.clothingID == clothingID);
        if (clothingBag == null)
        {
            return;
        }

        clothingFittingUI.ShowData(clothingBag);
    }

    private void ClearClothingAssetSlots()
    {
        foreach (var assetsSlot in _clothingBags)
        {
            if (assetsSlot != null)
            {
                assetsSlot.Release();
                AssetsManager.Instance.FreeGameObject(assetsSlot.gameObject);
            }
        }
        _clothingBags.Clear();
    }

    #endregion
    
}
