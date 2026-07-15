using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Localization.Components;
using XFramework;

public class MakeFoodResTip : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText, contentText;
    [SerializeField] LocalizeStringEvent nameLse, contentLse;
    [SerializeField] Image iconImage;
    
    public void ShowTip(string key, ItemInfo info)
    {
        Debug.Log(key);
        Debug.Log(info == null);
        
        if(info == null)
        {
            nameText.text = string.Empty;
            contentText.text = string.Empty;
            iconImage.ClearIcon();
            gameObject.SetActive(false);
            return;
        }
        ItemData itemData = InventoryManager.Instance.GetItemData(info.ID);
        nameLse.SetText(itemData.NameKey.Table, itemData.NameKey.Value);
        string itemName = LanguageManager.Instance.GetLocalizedString(itemData.NameKey.Table, info.GetNameKey());
        contentLse.SetTextWithVar(LocTableSet.Kitchen, key, LocVarSet.MiniGame1CookGame.ItemName, itemName);
        // nameText.text = info.Name;
        // contentText.text = content;
        iconImage.SetIcon(info.GetIconPath());
        gameObject.SetActive(true);
    }
}
