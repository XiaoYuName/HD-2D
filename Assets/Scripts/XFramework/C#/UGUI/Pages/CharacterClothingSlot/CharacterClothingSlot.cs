using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class CharacterClothingSlot : UIBase
{
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
