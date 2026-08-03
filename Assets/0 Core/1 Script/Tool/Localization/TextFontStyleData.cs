using TMPro;
using UnityEngine;

/// <summary>
/// 一种语言、一种样式的 TMP 字体资源包：字体 + 配套材质。
/// 每个「语言 × 样式」建一份放进本地化资源表，样式名作为条目 Key。
/// </summary>
[CreateAssetMenu(fileName = nameof(TextFontStyleData), menuName = "Loc/TextFontStyleData")]
public class TextFontStyleData : ScriptableObject
{
    [SerializeField] TMP_FontAsset font;
    [Tooltip("留空则用字体默认材质")]
    [SerializeField] Material material;

    public TMP_FontAsset Font => font;
    public Material Material => material;
    
    void OnValidate()
    {
        if (font == null)
            return;

        // 保留基于当前字体图集制作的自定义材质（如描边材质），
        // 但字体更换后若材质仍指向旧图集，则恢复为新字体的默认材质。
        if (material == null || !IsCompatibleMaterial(material, font))
            material = font.material;
    }

    static bool IsCompatibleMaterial(Material candidate, TMP_FontAsset targetFont)
    {
        if (candidate == targetFont.material)
            return true;

        Texture targetAtlas = targetFont.atlasTextures is { Length: > 0 } atlases
            ? atlases[0]
            : targetFont.material != null && targetFont.material.HasProperty(ShaderUtilities.ID_MainTex)
                ? targetFont.material.GetTexture(ShaderUtilities.ID_MainTex)
                : null;

        return targetAtlas != null &&
               candidate.HasProperty(ShaderUtilities.ID_MainTex) &&
               candidate.GetTexture(ShaderUtilities.ID_MainTex) == targetAtlas;
    }
}
