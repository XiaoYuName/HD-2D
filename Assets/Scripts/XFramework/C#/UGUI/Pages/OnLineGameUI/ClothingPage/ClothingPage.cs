using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class ClothingPage : UIBase
{
    private List<ClothingSlot> clothingSlots = new List<ClothingSlot>();
    
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
        CharacterManager.Instance.RegisterCharacterBagChange(GameCostTools.MainCharacterID,UpdateClothingSlot);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        foreach (var clothingSlot in clothingSlots)
        {
            clothingSlot.Release();
            AssetsManager.Instance.FreeGameObject(clothingSlot.gameObject);
        }
        clothingSlots.Clear();
        CharacterManager.Instance.UnregisterCharacterBagChange(GameCostTools.MainCharacterID,UpdateClothingSlot);
    }


    private void UpdateClothingSlot(CharacterBag characterBag)
    {
        var CharacterData = CharacterManager.Instance.GetCharacterDataByID(characterBag.CharacterID);
        if (CharacterData == null) return;
        foreach (var clothingSlot in clothingSlots)
        {
            clothingSlot.Release();
            AssetsManager.Instance.FreeGameObject(clothingSlot.gameObject);
        }
        clothingSlots.Clear();
        foreach (var clothingID in CharacterData.ClothingList)
        {
            ClothingData clothingData = LubanManager.Instance.TbClothingData.Get(clothingID);
            if(clothingData==null)continue;
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.ClothingSlotPath);
            obj.transform.SetParent(scrollView.content);
            obj.transform.localScale = Vector3.one;
            
            
            var slot = obj.GetComponent<ClothingSlot>();
            slot.Init();
            slot.SetData(clothingData,EquipCharacterClothing);
            slot.SetSelected(clothingID == characterBag.ClothingID);
            clothingSlots.Add(slot);
        }
    }

    private void EquipCharacterClothing(ClothingSlot clothingSlot)
    {
        CharacterManager.Instance.EquipCharacterClothing(GameCostTools.MainCharacterID,clothingSlot.ClothingData.ID);
    }
}
