using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Components;

/// <summary>
/// 「游戏情况」战况列表中的单行：图标 + 名称(多语言) + 数量。
/// </summary>
public class FactoryStatCell : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] LocalizeStringEvent labelLse;
    [SerializeField] TMP_Text valueText;

    public void Set(Sprite icon, string table, string labelKey, string value, Color valueColor)
    {
        iconImage.sprite = icon;
        labelLse.SetText(table, labelKey);
        valueText.text = value;
        valueText.color = valueColor;
    }
}
