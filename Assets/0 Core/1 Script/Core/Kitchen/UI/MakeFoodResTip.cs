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
    // Coroutine hideCt;

    public void ShowTip(string key, ItemStack info)
    {
        Debug.Log(key);
        Debug.Log(info == null);
        
        if(info == null)
        {
            nameText.text = string.Empty;
            contentText.text = string.Empty;
            iconImage.sprite = null;
            gameObject.SetActive(false);
            return;
        }

        ItemData itemData = InventoryManager.Instance.GetItemData(info.ID);

        nameLse.SetText(itemData.NameKey.Table, itemData.NameKey.Value);

        // MakeFoodSuccess = "{ItemName} 制作成功"：先灌好 ItemName 占位符再切引用，
        // 否则 SetReference 会立刻按当前(空)占位符格式化一次，SmartFormat 抛 FormattingException。
        // info.Name 是 InventoryItem 表的 key，这里取它在当前语言下的成品名喂给占位符；
        // MakeFoodFail 无占位符，多灌的 ItemName 会被忽略，无副作用。
        string itemName = LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, info.NameKey);
        contentLse.SetTextWithVar(LocTableSet.Kitchen, key, LocVarSet.MiniGame1CookGame.ItemName, itemName);
        // nameText.text = info.Name;
        // contentText.text = content;
        iconImage.SetIcon(itemData.IconName);
        gameObject.SetActive(true);

        // if(hideCt != null)
        //     StopCoroutine(hideCt);
            
        // hideCt = StartCoroutine(HideAfterDelay(2f));
    }

    // IEnumerator HideAfterDelay(float delay)
    // {
    //     yield return new WaitForSeconds(delay);
    //     gameObject.SetActive(false);
    // }
}
