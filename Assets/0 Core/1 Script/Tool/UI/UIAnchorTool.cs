using UnityEngine;
using UnityEngine.UI;

public static class UIAnchorTool
{
    /// <summary>
    /// 将 RectTransform 当前的位置与尺寸烘焙成「父物体占比」锚点：anchorMin/Max 用比例、offset 归零。
    /// 烘焙后该节点始终占父物体同等比例，父物体改宽高时同比缩放（适用于无旋转/缩放的 UI 节点）。
    /// 返回是否成功（无父 RectTransform 或父物体尺寸为 0 时返回 false）。
    /// </summary>
    public static bool SetProportionalAnchors(RectTransform rt)
    {
        if(rt == null || !(rt.parent is RectTransform parent))
            return false;

        Rect pr = parent.rect;
        if(pr.width == 0f || pr.height == 0f)
            return false;

        // 取四角（相对自身pivot）转换到父物体本地坐标（无旋转/缩放）
        Vector3[] corners = new Vector3[4];
        rt.GetLocalCorners(corners);   // [0]=左下 [2]=右上
        Vector2 pivotPos = rt.localPosition;   // 自身pivot在父物体本地坐标中的位置
        Vector2 min = pivotPos + (Vector2)corners[0];
        Vector2 max = pivotPos + (Vector2)corners[2];

        rt.anchorMin = new Vector2((min.x - pr.xMin) / pr.width, (min.y - pr.yMin) / pr.height);
        rt.anchorMax = new Vector2((max.x - pr.xMin) / pr.width, (max.y - pr.yMin) / pr.height);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return true;
    }

    // 将 RectTransform 锚点拉伸为铺满父物体（四边贴合）
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 以父物体左上角为基准定位（锚点/轴心均取左上角）
    public static void TopLeft(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
    }
}
