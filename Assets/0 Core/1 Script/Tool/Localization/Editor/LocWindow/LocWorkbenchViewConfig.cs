#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>多语言工作台的固定视图资源配置。</summary>
[CreateAssetMenu(fileName = "LocWorkbenchViewConfig", menuName = "Tools/Loc/Workbench View Config")]
public class LocWorkbenchViewConfig : ScriptableObject
{
    public VisualTreeAsset workbenchUxml;
    public StyleSheet workbenchUss;

    static LocWorkbenchViewConfig st;

    /// <summary>返回工程中唯一的工作台视图配置；缺失或重复时输出错误并返回 null。</summary>
    public static LocWorkbenchViewConfig St
    {
        get
        {
            if(st != null)
                return st;
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(LocWorkbenchViewConfig)}");
            if(guids.Length != 1)
            {
                Debug.LogError($"[Loc工作台] 需要唯一的 {nameof(LocWorkbenchViewConfig)} 资产，当前找到 {guids.Length} 个。");
                return null;
            }
            st = AssetDatabase.LoadAssetAtPath<LocWorkbenchViewConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return st;
        }
    }

    void OnEnable() => st = this;

    void OnDisable()
    {
        if(st == this)
            st = null;
    }
}
#endif
