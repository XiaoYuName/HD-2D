using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace XFramework.Fish
{
    public class FishLogCellUI : MonoBehaviour
    {
        [SerializeField] Image icon, bg;
        [SerializeField] GameObject starPrefab;
        [SerializeField] List<GameObject> starList;
        [SerializeField] Transform startContainer;
        [SerializeField] LocalizeStringEvent nameText;
        [SerializeField] TextMeshProUGUI lengthText, weightText;

        static readonly Dictionary<ItemQuality, Color> QualityBgColorDict = new()
        {
            {ItemQuality.A , new Color(0.847f, 0.847f, 0.847f)}, // #D8D8D8
            {ItemQuality.B , new Color(0.847f, 0.847f, 0.847f)}, // #D8D8D8
            {ItemQuality.C , new Color(0.925f, 0.851f, 0.643f)}, // #ECD9A4
            {ItemQuality.S , new Color(0.843f, 0.718f, 0.890f)}, // #D7B7E3
            {ItemQuality.SSR , new Color(0.843f, 0.718f, 0.890f)}// #D7B7E3
        };

        void Awake()
        {
            
        }

        public void Set(ItemInfo item, float length, float weight)
        {
            icon.SetIcon(item.GetIconPath());
            nameText.SetText(item.GetNameTable(), item.GetNameKey());
            bg.color = QualityBgColorDict[item.GetQuality()];

            foreach (GameObject star in starList)
                Destroy(star);

            starList.Clear();
            
            // 根据质量设置星数
            int starCount = (int)item.GetQuality();
            for (int i = 0; i < starCount; i++)
            {
                GameObject star = Instantiate(starPrefab, startContainer);
                star.SetActive(true);
                starList.Add(star);
            }
            if(item.GetMtType() != ItemMaterialType.FishingProduct)
            {
                lengthText.text = length.ToString("0.00") + "cm";
                weightText.text = weight.ToString("0.00") + "kg";
            }
            else
            {
                lengthText.text = "";
                weightText.text = "";
            }
        }
    }
}