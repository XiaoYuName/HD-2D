using TMPro;
using UnityEngine;

/// <summary>
/// 一种语言、一种样式的 TMP 字体资源包：字体 + 配套材质。
/// 每个「语言 × 样式」建一份放进本地化资源表，样式名作为条目 Key。
/// </summary>
[CreateAssetMenu(fileName = nameof(TextFontStyleData), menuName = "Loc/TextFontStyleData")]
public class TextFontStyleData : ScriptableObject
{
    public TMP_FontAsset font;
    [Tooltip("留空则用字体默认材质")]
    public Material material;

    public Material Material => material != null ? material : font != null ? font.material : null;
}
