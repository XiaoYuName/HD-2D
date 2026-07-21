using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace XFramework.Fish
{
    public class FishResultCellUI : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] GameObject starPrefab;
        [SerializeField] List<GameObject> starList;
        [SerializeField] Transform startContainer;
        [SerializeField] LocalizeStringEvent nameText;
        [SerializeField] LocalizeStringEvent descText;
        [SerializeField] TextMeshProUGUI lengthText, weightText;
        
        void Awake()
        {
            
        }

        public void Set(ItemInfo item, float length, float weight)
        {
            foreach (GameObject star in starList)
                Destroy(star);
            starList.Clear();

            icon.SetIcon(item.GetIconPath());
            nameText.SetText(item.GetNameTable(), item.GetNameKey());
            descText.SetText(item.GetDescTable(), item.GetDescKey());
            
            // 根据质量设置星数
            int starCount = (int)item.GetQuality();
            for (int i = 0; i < starCount; i++)
            {
                GameObject star = Instantiate(starPrefab, startContainer);
                star.SetActive(true);
                starList.Add(star);
            }
            // 判断是鱼还是垃圾
            Debug.Log(item.GetMtType());
            if(item.GetMtType() != ItemMaterialType.FishingProduct)
            {
                lengthText.text = length.ToString("0.00") + " cm";
                weightText.text = weight.ToString("0.00") + " kg";
            }
            else
            {
                lengthText.text = "";
                weightText.text = "";
            }
        }
    }
}
