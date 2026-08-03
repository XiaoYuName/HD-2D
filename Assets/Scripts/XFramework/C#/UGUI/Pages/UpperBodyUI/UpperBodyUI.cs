using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class UpperBodyUI : UIBase
{
    private CharacterBag characterBag;
    private ClothingBag clothingBag;
    private ClothingData clothingData;

    private List<UpperSlot> upperSlots =new List<UpperSlot>();
    private EquipClothingSlot equipClothingSlot;
    private UpperSlot selectedUpperSlot;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,string.Empty);
    }

    public override void Release()
    {
        foreach (var slot in upperSlots)
        {
            slot.Close();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        upperSlots.Clear();
        selectedUpperSlot = null;
        ClearEquipClothingSlot();
        base.Release();
    }

    public void SetData(CharacterBag characterBag, ClothingBag clothingBag)
    {
        this.characterBag = characterBag;
        this.clothingBag = clothingBag;
        clothingData = LubanManager.Instance.TbClothingData.GetOrDefault(clothingBag.clothingID);
        if (clothingData != null)
        {
            foreach (var slot in upperSlots)
            {
                slot.Close();
                AssetsManager.Instance.FreeGameObject(slot.gameObject);
            }
            upperSlots.Clear();
            selectedUpperSlot = null;
            ClearEquipClothingSlot();

            CreateUpperSlot();
        }
    }

    private void CreateUpperSlot()
    {
        foreach (var accessorID in clothingData.AccessoriesList)
        {
            ClothingAccessoriesData accessoriesData =
                LubanManager.Instance.TbClothingAccessoriesData.GetOrDefault(accessorID);
            if (accessoriesData != null)
            {
               var obj =  AssetsManager.Instance.Instantiate(AssetKeys.UpperSlotPath);
               obj.transform.SetParent(slotContent);
               obj.transform.localScale = Vector3.one;
               var slot = obj.transform.GetComponent<UpperSlot>();
               slot.Init();
               slot.SetData(accessoriesData);
               slot.SetSelected(false);
               upperSlots.Add(slot);
            }
        }
    }

    /// <summary>
    /// 点击下方的 UpperSlot: 选中它,并在 background 中心生成对应的 EquipClothingSlot。
    /// 同一时间只保留一个,选中新的之前先把上一个删掉。
    /// </summary>
    public void SelectedUpperSlot(UpperSlot slot)
    {
        if (slot == null || selectedUpperSlot == slot)
        {
            return;
        }

        if (selectedUpperSlot != null)
        {
            selectedUpperSlot.SetSelected(false);
        }
        ClearEquipClothingSlot();

        selectedUpperSlot = slot;
        selectedUpperSlot.SetSelected(true);
        CreateEquipClothingSlot(slot.AccessoriesData);
    }

    /// <summary>
    /// 在 background 中心生成 EquipClothingSlot
    /// </summary>
    private EquipClothingSlot CreateEquipClothingSlot(ClothingAccessoriesData data)
    {
        if (data == null || background == null)
        {
            return null;
        }

        var obj = AssetsManager.Instance.Instantiate(AssetKeys.EquipClothingSlotPath);
        obj.transform.SetParent(background, false);
        obj.transform.localScale = Vector3.one;

        var slot = obj.GetComponent<EquipClothingSlot>();
        slot.Init();
        slot.Rect.anchorMin = new Vector2(0.5f, 0.5f);
        slot.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        slot.Rect.pivot = new Vector2(0.5f, 0.5f);
        slot.Rect.localEulerAngles = Vector3.zero;
        slot.SetData(data);
        // 素材四周有大片透明留白,按图片实际内容居中,而不是按整个 Rect 居中
        slot.Rect.anchoredPosition = -slot.ContentLocalCenter;
        equipClothingSlot = slot;
        return slot;
    }

    private void ClearEquipClothingSlot()
    {
        if (equipClothingSlot == null)
        {
            return;
        }

        equipClothingSlot.Release();
        AssetsManager.Instance.FreeGameObject(equipClothingSlot.gameObject);
        equipClothingSlot = null;
    }
}
