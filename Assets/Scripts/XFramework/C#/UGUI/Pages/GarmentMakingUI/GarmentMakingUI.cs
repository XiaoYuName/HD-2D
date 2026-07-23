using System.Collections.Generic;
using System.Linq;
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
    }

    private OptionType optionType;

    public override void Init()
    {
        InitAutoBind();
        clothingFittingUI.Init();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        commonTopUI.Init();
        commonTopUI.SetTitle(uiPageData.PageID,"Title");
        commonTopUI.SetClose(Close);
        optionType = OptionType.Node;
        Bind(starButton, StartProductionClothing,"");
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
        foreach (var assetsSlot in _clothingBags)
        {
            AssetsManager.Instance.FreeGameObject(assetsSlot.gameObject);
        }
    }
    
    private void SelectedCharacterBagChange(CharacterBag characterBag)
    {
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
    }

    private ClothingAssetsSlot selectedClothingAssetsSlot;
    private void OnSelectedClothingAssetsSlot(ClothingAssetsSlot slot)
    {
        if (selectedClothingAssetsSlot == slot)
        {
            selectedClothingAssetsSlot.SetSelected(false);
            selectedClothingAssetsSlot = null;
            starButton.interactable = false;
            return;
        }

        foreach (var assetsSlot in _clothingBags)
        {
            if (assetsSlot == slot)
            {
                selectedClothingAssetsSlot = assetsSlot;
                selectedClothingAssetsSlot.SetSelected(true);
                starButton.interactable = true;
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
        if (type == OptionType.Clothing)
        {
            viewPanel.gameObject.SetActive(true);
            clothingFittingUI.Close();
        }
        else if (type == OptionType.Info)
        {
            viewPanel.gameObject.SetActive(false);
            clothingFittingUI.Open();
        }

    }

    #endregion
    
}
