using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class FactoryMoldMgLeftUpItemUI : MonoBehaviour
{
    [SerializeField] FactoryMoldItemIcon iconImage;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text countText;
    [SerializeField] LocTextVar priceText;

    public void Set(FactoryMoldItemInfo itemInfo)
    {
        if(itemInfo == null)
        {
            iconImage.gameObject.SetActive(false);
            countText.text = "";
            nameText.text = "";
            return;
        }

        iconImage.gameObject.SetActive(true);
        iconImage.Set(itemInfo);
        countText.text = "x" + itemInfo.Count.ToString();
        nameText.text = LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, itemInfo.PaintingNameKey) + LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, itemInfo.FrameNameKey);
        priceText.SetVar(LocVarSet.FactoryMain.Price, itemInfo.Value);
    }
}
