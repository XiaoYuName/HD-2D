using UnityEngine;

/// <summary>
/// GameObject / Component 常用操作的静态工具类
/// </summary>
public static class GameObjectTool
{
    /// <summary>
    /// 设置组件所在 GameObject 的激活状态（状态未变化时跳过，避免无意义的 SetActive 调用）
    /// </summary>
    public static void SetActive(Component comp, bool active)
    {
        if(comp.gameObject.activeSelf != active)
            comp.gameObject.SetActive(active);
    }

    /// <summary>
    /// 设置 GameObject 的激活状态（状态未变化时跳过）
    /// </summary>
    public static void SetActive(GameObject go, bool active)
    {
        if(go.activeSelf != active)
            go.SetActive(active);
    }
}
