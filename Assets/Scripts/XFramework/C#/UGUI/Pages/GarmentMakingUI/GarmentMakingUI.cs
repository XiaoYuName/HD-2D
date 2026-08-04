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

    /// <summary>
    /// 左侧 CharacterNormal 里动态加载的装配预制体，以及它当前显示的服装ID。
    /// 每件服装一套，身体部件位置都不一样。
    /// </summary>
    private CharacterClothingSlot characterClothingSlot;
    private long characterClothingID;

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
        // 关面板时不会复位淡入淡出，这里显式回到"没选服装"的状态，免得重开时停在上次的透明度上
        FadeSequence?.Kill();
        nodeFace.alpha = 1;
        characterNormal.alpha = 0;
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
        ClearCharacterClothingSlot();
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
           // 选中态每个槽位都显式设一次,不留"新实例默认就是未选中"这种依赖:
           // 槽位走对象池复用,漏设的那个会把上一次的选中描边带过来,看着就是多选
           cloth.SetSelected(clothingBag.clothingID == selectedClothingID);
           cloth.OnSelect.RemoveAllListeners();
           cloth.OnSelect.AddListener(OnSelectedClothingAssetsSlot);
           _clothingBags.Add(cloth);
       }

       if (selectedClothingID > 0)
       {
           selectedClothingAssetsSlot = _clothingBags.Find(slot => slot.CurrentBag.clothingID == selectedClothingID);
           if (selectedClothingAssetsSlot != null)
           {
               starButton.interactable = true;
           }
       }

       // 刚解锁的配件要让左侧角色跟着穿上
       ShowCharacterClothing(selectedClothingAssetsSlot != null
           ? selectedClothingAssetsSlot.CurrentBag.clothingID
           : 0);

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
        // 再点一次已经选中的槽位 = 取消选中
        selectedClothingAssetsSlot = selectedClothingAssetsSlot == slot ? null : slot;

        // 选中态先一次性刷完,再去做加载和动画。
        // 原来这两件事混在同一个循环里:命中的那个槽位选上之后紧接着 ShowCharacterClothing,
        // 它一抛异常(比如服装装配预制体加载失败)循环就断在这儿,后面的槽位漏掉了没取消,
        // 表现就是同时有好几个槽位是选中的。
        foreach (var assetsSlot in _clothingBags)
        {
            assetsSlot.SetSelected(assetsSlot == selectedClothingAssetsSlot);
        }

        starButton.interactable = selectedClothingAssetsSlot != null;
        ShowCharacterClothing(selectedClothingAssetsSlot != null
            ? selectedClothingAssetsSlot.CurrentBag.clothingID
            : 0);
        FadeCharacterNormal(selectedClothingAssetsSlot != null);
    }

    /// <summary>
    /// 左侧的角色立绘和空衣架二选一淡入淡出。
    /// 每次都重建 Sequence：往已经播完或者被 Kill 掉的 Sequence 上 Append 是不会播的，
    /// 而且第一次选中服装时它还是 null。
    /// </summary>
    private void FadeCharacterNormal(bool showCharacter)
    {
        FadeSequence?.Kill();
        FadeSequence = DOTween.Sequence();
        FadeSequence.Append(nodeFace.DOFade(showCharacter ? 0 : 1, 0.3f));
        FadeSequence.Append(characterNormal.DOFade(showCharacter ? 1 : 0, 0.3f));
    }

    /// <summary>
    /// 左侧角色按这件服装显示：已解锁的配件正常显示，没解锁的完全不显示（连轮廓也不显示）。
    /// 传 0 表示当前没有选中服装，把角色身上的东西清掉。
    /// 同一件服装重复调用只刷新装配状态，不会重新实例化，所以解锁配件后可以直接再调一次。
    /// </summary>
    public void ShowCharacterClothing(long clothingID)
    {
        ClothingBag clothingBag = clothingID > 0
            ? CharacterManager.Instance.GetClothingBag(GameCostTools.MainCharacterID, clothingID)
            : null;
        if (clothingBag == null)
        {
            ClearCharacterClothingSlot();
            return;
        }

        if (characterClothingSlot == null || characterClothingID != clothingID)
        {
            ClearCharacterClothingSlot();

            ClothingData clothingData = LubanManager.Instance.TbClothingData.GetOrDefault(clothingID);
            characterClothingSlot = CharacterClothingSlot.Create(clothingData, characterNormal.transform);
            if (characterClothingSlot == null)
            {
                return;
            }
            characterClothingID = clothingID;
        }

        // 这里不调 BlinkAccessories：没解锁的部件在未装配态本来就是全透明的，
        // 只有闪烁提示时才会把轮廓显出来，所以不闪就不会有轮廓
        characterClothingSlot.SetEquippedAccessories(
            CharacterManager.Instance.GetUnlockedAccessoriesIDs(clothingBag));
    }

    private void ClearCharacterClothingSlot()
    {
        CharacterClothingSlot.Free(characterClothingSlot);
        characterClothingSlot = null;
        characterClothingID = 0;
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

    public void OptionClothing()
    {
        Option(OptionType.Clothing);
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
