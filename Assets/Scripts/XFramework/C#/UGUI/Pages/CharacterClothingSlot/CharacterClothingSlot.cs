using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class CharacterClothingSlot : UIBase
{
    /// <summary>
    /// 按服装配置动态加载装配预制体：身体部件的位置和数量每件服装都不一样，
    /// 所以不挂在面板里，走 ClothingData.CharacterClothingSlotPath。
    /// 失败返回 null（日志里已经打清楚原因）。
    /// </summary>
    public static CharacterClothingSlot Create(ClothingData clothingData, Transform parent)
    {
        if (clothingData == null || parent == null)
        {
            Debug.LogError("加载服装装配预制体缺少服装配置或挂载节点");
            return null;
        }

        if (string.IsNullOrEmpty(clothingData.CharacterClothingSlotPath))
        {
            Debug.LogError($"服装 {clothingData.ID} 没有配置服装装配预制体(CharacterClothingSlotPath)");
            return null;
        }

        var obj = AssetsManager.Instance.Instantiate(clothingData.CharacterClothingSlotPath);
        if (obj == null)
        {
            Debug.LogError($"服装装配预制体加载失败，ClothingID: {clothingData.ID}, Path: {clothingData.CharacterClothingSlotPath}");
            return null;
        }

        // 预制体自己带好了锚点和位置,SetParent 保留它的布局
        obj.transform.SetParent(parent, false);
        obj.transform.localScale = Vector3.one;

        var slot = obj.GetComponent<CharacterClothingSlot>();
        if (slot == null)
        {
            Debug.LogError($"服装装配预制体上没有 CharacterClothingSlot 组件，Path: {clothingData.CharacterClothingSlotPath}");
            AssetsManager.Instance.FreeGameObject(obj);
            return null;
        }

        // FreeGameObject 只是回池,复用到的实例还带着上一次的装配状态;
        // Init 会把所有部件退回未装配(也就是完全不显示),之后再按已解锁配件重新摆
        slot.Init();
        return slot;
    }

    /// <summary>回收 Create 出来的实例。调用方记得把自己的引用置空。</summary>
    public static void Free(CharacterClothingSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        slot.Release();
        AssetsManager.Instance.FreeGameObject(slot.gameObject);
    }

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        foreach (var slot in bodyAccessoriesList)
        {
            if (slot != null)
            {
                slot.Init();
            }
        }
    }

    /// <summary>
    /// 让配件ID匹配的部件闪烁,其余部件停止闪烁
    /// </summary>
    public void BlinkAccessories(long accessoriesID)
    {
        foreach (var slot in bodyAccessoriesList)
        {
            if (slot != null)
            {
                slot.SetBlink(slot.AccessoriesID == accessoriesID);
            }
        }
    }

    public void StopBlink()
    {
        foreach (var slot in bodyAccessoriesList)
        {
            if (slot != null)
            {
                slot.SetBlink(false);
            }
        }
    }

    /// <summary>
    /// 找拖拽落点命中的部件: 配件ID要对得上、还没装配过,且落点在吸附范围内。
    /// 多个候选取最近的一个(落点都在部件内时按中心距离比)。
    /// </summary>
    public BodyAccessoriesSlot FindDropTarget(long accessoriesID, Vector3 worldPoint)
    {
        BodyAccessoriesSlot target = null;
        var nearestEdge = float.MaxValue;
        var nearestCenter = float.MaxValue;
        foreach (var slot in bodyAccessoriesList)
        {
            if (slot == null || slot.IsEquipped || slot.AccessoriesID != accessoriesID)
            {
                continue;
            }

            if (!slot.IsInDropRange(worldPoint))
            {
                continue;
            }

            var edgeDistance = slot.GetDropDistance(worldPoint);
            var centerDistance = (slot.Rect.position - worldPoint).sqrMagnitude;
            if (edgeDistance > nearestEdge || (Mathf.Approximately(edgeDistance, nearestEdge) && centerDistance >= nearestCenter))
            {
                continue;
            }

            nearestEdge = edgeDistance;
            nearestCenter = centerDistance;
            target = slot;
        }

        return target;
    }

    /// <summary>
    /// 按配件ID批量设置装配状态: 列表里的部件正常显示,其余的还原成只显示轮廓。
    /// 服装上身一次只做一个配件,之前做好的配件要保持穿在身上。
    /// </summary>
    public void SetEquippedAccessories(ICollection<long> equippedAccessoriesIDs)
    {
        foreach (var slot in bodyAccessoriesList)
        {
            if (slot != null)
            {
                slot.SetEquipped(equippedAccessoriesIDs != null && equippedAccessoriesIDs.Contains(slot.AccessoriesID));
            }
        }
    }

    /// <summary>
    /// 全部还原成未装配(只显示轮廓)状态
    /// </summary>
    public void ResetAll()
    {
        SetEquippedAccessories(null);
    }

    public override void Release()
    {
        StopBlink();
        base.Release();
    }
}
