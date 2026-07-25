using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XFramework;

public partial class ClothingFittingUI : UIBase
{
    private List<ClothingBag> selectedClothingAssetsSlot = new List<ClothingBag>();
    private int selectedIndex;
    public ClothingData CurrentData { get; private set; }
    public ClothingBag CurrentBag { get; private set; }

    private List<AccessoriesSlot> selectedAccessoriesSlot = new List<AccessoriesSlot>();
    
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(indexDownButton,OnDownShowData,"");
        Bind(indexUpButton,OnUpShowData,"");
        Bind(starMinGameButton,StartMinGameOnClick,"");
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        foreach (var Slot in selectedAccessoriesSlot)
        {
            Slot.Close();
            AssetsManager.Instance.FreeGameObject(Slot.gameObject);
        }
        selectedAccessoriesSlot.Clear();
    }

    public void ShowData(ClothingBag clothingBag)
    {
        indexTex.text = $"{selectedIndex + 1}";
        
        foreach (var Slot in selectedAccessoriesSlot)
        {
            Slot.Close();
            AssetsManager.Instance.FreeGameObject(Slot.gameObject);
        }
        selectedAccessoriesSlot.Clear();
        
        ClothingData clothingData = LubanManager.Instance.TbClothingData.Get(clothingBag.clothingID);
        CurrentData = clothingData;
        CurrentBag = clothingBag;
        foreach (var AccessoriesList in clothingData.AccessoriesList)
        {
            ClothingAccessoriesData accessoriesData = LubanManager.Instance.TbClothingAccessoriesData.Get(AccessoriesList);
            if (accessoriesData != null)
            {
               var obj =  AssetsManager.Instance.Instantiate(AssetKeys.AccessoriesSlotPath);
               obj.transform.SetParent(scrollView.content);
               obj.transform.localScale = Vector3.one;

               var slot = obj.transform.GetComponent<AccessoriesSlot>();
               slot.Init();
               slot.SetData(clothingBag,accessoriesData);
               slot.Open();
               selectedAccessoriesSlot.Add(slot);
            }
        }

        starMinGameButton.interactable = clothingBag.isUnlock;
    }

    public void SetDataList(List<ClothingBag> clothingList,int selected)
    {
        selectedClothingAssetsSlot = clothingList;
        selectedIndex  = selected;
        ShowData(selectedClothingAssetsSlot[selectedIndex]);
        
    }

    public void OnUpShowData()
    {
        selectedIndex -= 1;
        if (selectedIndex < 0)
        {
            selectedIndex = selectedClothingAssetsSlot.Count - 1;
        }

        ShowData(selectedClothingAssetsSlot[selectedIndex]);
    }

    public void OnDownShowData()
    {
        selectedIndex += 1;
        if (selectedIndex > selectedClothingAssetsSlot.Count - 1)
        {
            selectedIndex = 0;
        }
        ShowData(selectedClothingAssetsSlot[selectedIndex]);
    }

    public void StartMinGameOnClick()
    {
        CharacterManager.Instance.Execute(CurrentData.MinGameType,CharacterManager.Instance.GetCharacterBag(GameCostTools.MainCharacterID)
        ,CurrentBag);
    }
}
