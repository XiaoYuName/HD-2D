using UnityEditor;
using UnityEngine.UIElements;

/// <summary>Texture Tools 共享 USS 主题入口，模块只负责结构与动态状态。</summary>
internal static class TextureToolsTheme
{
    private const string StylePath = "Assets/0 Core/1 Script/Tool/Editor/Texture/TextureToolsTheme.uss";
    private static StyleSheet styleSheet;

    internal static void Apply(VisualElement root)
    {
        styleSheet ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
        if (styleSheet != null && !root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);
        root.AddToClassList("texture-tools-root");
    }
}
