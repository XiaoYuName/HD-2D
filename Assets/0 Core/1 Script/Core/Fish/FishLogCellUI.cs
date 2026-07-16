using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using TMPro;
using XFramework;
using System.Collections.Generic;

public class FishLogCellUI : MonoBehaviour
{
    [SerializeField] Image icon, bgImage;
    [SerializeField] Transform startContainer;
    [SerializeField] GameObject[] starList;
    [SerializeField] LocalizeStringEvent nameText;
    [SerializeField] TextMeshProUGUI lengthText, weightText;

    static readonly Dictionary<ItemQuality, Color> QualityBgColorDict = new()
    {
        {ItemQuality.A , new Color(0.2f, 0.8f, 0.2f)},// #D8D8D8
        {ItemQuality.B , new Color(0.4f, 0.4f, 0.4f)},  // #D8D8D8
        {ItemQuality.C , new Color(0.6f, 0.6f, 0.6f)},  // #ECD9A4
        {ItemQuality.S , new Color(0.8f, 0.8f, 0.2f)},  // #D7B7E3
        {ItemQuality.SSR , new Color(0.8f, 0.2f, 0.2f)} // #D7B7E3
    };

    void Awake()
    {
        
    }

    public void Set(ItemInfo item, float length, float weight)
    {
        icon.SetIcon(item.GetIconPath());
        nameText.SetText(item.GetNameTable(), item.GetNameKey());
        bgImage.color = QualityBgColorDict[item.GetQuality()];
        // 根据质量设置星数
        for (int i = 0; i < starList.Length; i++)
        {
            starList[i].SetActive(i <= (int)item.GetQuality() - 1);
        }
        lengthText.text = length.ToString("0.00") + "cm";
        weightText.text = weight.ToString("0.00") + "kg";
    }
}
