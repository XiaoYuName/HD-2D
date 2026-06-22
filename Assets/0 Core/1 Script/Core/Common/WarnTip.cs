using TMPro;
using UnityEngine;
using System.Collections;
using UnityEngine.Localization.Components;

public class WarnTip : MonoBehaviour
{
    [SerializeField] LocalizeStringEvent contentText;
    Coroutine hideCt;

    void OnDisable()
    {
        Close();
    }
    public void ShowTip(string table, string key)
    {
        contentText.SetText(table, key);
        gameObject.SetActive(true);
        if(hideCt != null)
            StopCoroutine(hideCt);
            
        hideCt = StartCoroutine(HideAfterDelay(2f));
    }
    public void Close()
    {
        if(hideCt != null)
            StopCoroutine(hideCt);
            
        gameObject.SetActive(false);
    }

    IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
    }
}
