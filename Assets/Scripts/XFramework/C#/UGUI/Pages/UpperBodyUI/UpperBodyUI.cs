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
    private bool isCompleted;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,string.Empty);
        characterClothingSlot.Init();
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
        characterClothingSlot.StopBlink();
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
            isCompleted = false;
            ClearEquipClothingSlot();
            // 换一套服装重新开始,身上的部件全部退回只显示轮廓
            characterClothingSlot.ResetAll();

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
               slot.SetIsComplete(false);
               upperSlots.Add(slot);
            }
        }
    }

    /// <summary>
    /// 点击下方的 UpperSlot: 选中它,在 background 中心生成对应的 EquipClothingSlot,
    /// 同时让身上对应的部件闪烁提示可以拖过去装配。
    /// 同一时间只保留一个 EquipClothingSlot,选中新的之前先把上一个删掉。
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
        characterClothingSlot.BlinkAccessories(slot.AccessoriesData.ID);
    }

    /// <summary>
    /// 把 EquipClothingSlot 拖到闪烁的部件上松手: 命中就装配,该部件从只显示轮廓变成正常显示。
    /// 返回 true 表示已装配并回收了这个 EquipClothingSlot。
    /// </summary>
    public bool TryEquipClothingSlot(EquipClothingSlot slot)
    {
        if (slot == null || slot.Data == null)
        {
            return false;
        }

        var target = characterClothingSlot.FindDropTarget(slot.Data.ID, slot.GetContentWorldCenter());
        if (target == null)
        {
            return false;
        }

        // 先停闪烁再装配,避免 StopBlink 把刚装配好的部件又刷回静止态
        characterClothingSlot.StopBlink();
        target.SetEquipped(true);

        if (selectedUpperSlot != null)
        {
            selectedUpperSlot.SetSelected(false);
            // 已装配的配件不能再被选中
            selectedUpperSlot.SetIsComplete(true);
            selectedUpperSlot = null;
        }
        ClearEquipClothingSlot();
        CheckCompleted();
        return true;
    }

    /// <summary>
    /// 全部配件都装配完成后走服装小游戏通用结算流程
    /// </summary>
    private void CheckCompleted()
    {
        if (isCompleted || upperSlots.Count <= 0)
        {
            return;
        }

        foreach (var slot in upperSlots)
        {
            if (slot != null && !slot.IsComplete)
            {
                return;
            }
        }

        isCompleted = true;
        Debug.Log("服装上身完成!");
        UIUtility.PopClothingMinGameComplete(characterBag, clothingBag, ClothingMinGameType.UpperBody, Close);
    }

    public void MoveEquipClothingSlotToScreenPoint(EquipClothingSlot slot, Vector2 screenPosition, Camera eventCamera)
    {
        if (slot == null || background == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, screenPosition, eventCamera, out var localPosition))
        {
            // 跟随鼠标的是图片实际内容的中心,而不是带着大片透明留白的 Rect 中心
            slot.Rect.anchoredPosition = localPosition - slot.ContentLocalCenter;
        }
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
