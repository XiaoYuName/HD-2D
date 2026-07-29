using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

[RequireComponent(typeof(TMP_Text))]
[AddComponentMenu("Loc/LocTextFont")]
public sealed class LocTextFont : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] LocFontStyleConfig styleSetReference;

    public bool IsValid =>
        styleSetReference != null &&
        !styleSetReference.IsEmpty;

    void OnValidate()
    {
        text = GetComponent<TMP_Text>();
        styleSetReference?.Refresh();
    }

    void OnEnable()
    {
        styleSetReference.AssetChanged -= ApplyStyle;
        styleSetReference.AssetChanged += ApplyStyle;
    }

    void OnDisable()
    {
        styleSetReference.AssetChanged -= ApplyStyle;
    }
    void ApplyStyle(TextFontStyleData styleSet)
    {
        text.font = styleSet.font;
        text.fontSharedMaterial = styleSet.Material;
    }
}

[Serializable]
public sealed class LocFontStyleConfig : LocalizedAsset<TextFontStyleData>
{
    // 将 protected ForceUpdate 暴露给所属组件。
    public void Refresh()
    {
        ForceUpdate();
    }
}