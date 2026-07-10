using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            priceText.Clear();
            return;
        }

        iconImage.gameObject.SetActive(true);
        iconImage.Set(itemInfo);
        countText.text = "x" + itemInfo.Count.ToString();
        nameText.text = itemInfo.GetName();
        priceText.SetVar(LocVarSet.FactoryMain.Price, itemInfo.GetCost());
    }
}
