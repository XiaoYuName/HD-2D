using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class FactoryMerchandiseItemCellUI : MonoBehaviour
{
    [SerializeField] FactoryMoldItemIcon iconImage;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text countText;
    [SerializeField] LocTextVar priceText;

    public void Set(FactoryMerchandiseItemInfo item)
    {
        if(item == null)
        {
            iconImage.gameObject.SetActive(false);
            countText.text = "";
            nameText.text = "";
            priceText.Clear();
            return;
        }

        iconImage.gameObject.SetActive(true);
        iconImage.Set(item);
        countText.text = "x" + item.Count.ToString();
        nameText.text = item.GetName();
        priceText.SetVar(LocVarSet.FactoryMain.Price, item.GetValue());
    }
}
