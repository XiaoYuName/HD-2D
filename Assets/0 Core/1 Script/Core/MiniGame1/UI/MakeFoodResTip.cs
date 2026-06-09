using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class MakeFoodResTip : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText, contentText;
    [SerializeField] Image iconImage;
    // Coroutine hideCt;

    public void ShowTip(string content, ItemInfo info)
    {
        Debug.Log(content);
        Debug.Log(info == null);
        
        if(info == null)
        {
            nameText.text = string.Empty;
            contentText.text = string.Empty;
            iconImage.sprite = null;
            gameObject.SetActive(false);
            return;
        }
        nameText.text = info.Name;
        contentText.text = content;
        iconImage.sprite = info.Icon;

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
