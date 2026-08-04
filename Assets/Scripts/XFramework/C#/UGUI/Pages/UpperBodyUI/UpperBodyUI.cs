using System.Collections.Generic;
using UnityEngine;
using XFramework;

/// <summary>
/// 服装上身：服装打板结束后紧接着进这里，把刚打完板的那一个配件穿到角色身上，
/// 装配完成即解锁这个配件（四个配件都解锁后服装本身也会解锁）。
/// 一次只做一个配件，所以没有可选列表：进来就直接把这个配件的 EquipClothingSlot
/// 摆在 background 中心、身上对应部件开始闪烁，玩家拖过去装上即结算。
/// 之前做好的配件保持穿在身上。
/// </summary>
public partial class UpperBodyUI : UIBase
{
    private CharacterBag characterBag;
    private ClothingBag clothingBag;
    private ClothingData clothingData;
    private ClothingAccessoriesBag targetAccessoriesBag;

    private EquipClothingSlot equipClothingSlot;
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
        ClearEquipClothingSlot();
        characterClothingSlot.StopBlink();
        base.Release();
    }

    /// <summary>
    /// 本次要装配的配件由服装打板流程指定，只有它需要玩家拖上身。
    /// </summary>
    public void SetData(CharacterBag characterBag, ClothingBag clothingBag, ClothingAccessoriesBag accessoriesBag)
    {
        this.characterBag = characterBag;
        this.clothingBag = clothingBag;
        targetAccessoriesBag = accessoriesBag;
        clothingData = LubanManager.Instance.TbClothingData.GetOrDefault(clothingBag.clothingID);
        if (clothingData == null || accessoriesBag == null)
        {
            Debug.LogError($"服装上身缺少数据，ClothingID: {clothingBag.clothingID}, AccessoriesID: {accessoriesBag?.accessoriesID}");
            return;
        }

        ClothingAccessoriesData accessoriesData =
            LubanManager.Instance.TbClothingAccessoriesData.GetOrDefault(accessoriesBag.accessoriesID);
        if (accessoriesData == null)
        {
            Debug.LogError($"没有找到配件配置 {accessoriesBag.accessoriesID}，服装上身无法进行");
            return;
        }

        isCompleted = false;
        ClearEquipClothingSlot();
        // 之前已经做好的配件保持穿在身上,本次这一件和还没做的那些只显示轮廓
        characterClothingSlot.SetEquippedAccessories(GetUnlockedAccessoriesIDs());

        // 没有可选列表,直接把这一件摆出来并提示它该装到哪
        CreateEquipClothingSlot(accessoriesData);
        characterClothingSlot.BlinkAccessories(accessoriesData.ID);
    }

    /// <summary>
    /// 已经解锁（做完）的配件ID，本次要做的这一件不算在内。
    /// </summary>
    private HashSet<long> GetUnlockedAccessoriesIDs()
    {
        HashSet<long> unlockedIDs = new HashSet<long>();
        if (clothingBag?.Accessories == null) return unlockedIDs;

        foreach (var accessoriesBag in clothingBag.Accessories)
        {
            if (accessoriesBag == null || !accessoriesBag.isUnlock) continue;
            if (accessoriesBag.accessoriesID == targetAccessoriesBag.accessoriesID) continue;

            unlockedIDs.Add(accessoriesBag.accessoriesID);
        }

        return unlockedIDs;
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

        var target = characterClothingSlot.FindDropTarget(slot.Data.ID, slot.GetPivotWorldPosition());
        if (target == null)
        {
            return false;
        }

        // 先停闪烁再装配,避免 StopBlink 把刚装配好的部件又刷回静止态
        characterClothingSlot.StopBlink();
        target.SetEquipped(true);

        ClearEquipClothingSlot();
        CheckCompleted();
        return true;
    }

    /// <summary>
    /// 本次的配件装上身就算完成，直接走结算解锁这个配件
    /// </summary>
    private void CheckCompleted()
    {
        if (isCompleted)
        {
            return;
        }

        isCompleted = true;
        Debug.Log("服装上身完成!");
        UIUtility.PopClothingAccessoriesComplete(characterBag, clothingBag, targetAccessoriesBag, Close);
    }

    public void MoveEquipClothingSlotToScreenPoint(EquipClothingSlot slot, Vector2 screenPosition, Camera eventCamera)
    {
        if (slot == null || background == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, screenPosition, eventCamera, out var localPosition))
        {
            // 跟随鼠标的是配件自己的中心点(素材 pivot),而不是带着大片透明留白的 Rect 中心
            slot.Rect.anchoredPosition = localPosition - slot.PivotLocalPosition;
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
        // 素材四周有大片透明留白,按配件自己的中心点(素材 pivot)摆到 background 中心,
        // 而不是按整个 Rect 居中 —— 后者会让配件停在画布原本的位置上
        slot.Rect.anchoredPosition = -slot.PivotLocalPosition;
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
