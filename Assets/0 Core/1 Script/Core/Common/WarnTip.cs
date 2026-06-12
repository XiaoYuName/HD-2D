using TMPro;
using UnityEngine;
using System.Collections;

public class WarnTip : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI contentText;
    Coroutine hideCt;

    public void ShowTip(string content)
    {
        contentText.text = content;
        gameObject.SetActive(true);

        if(hideCt != null)
            StopCoroutine(hideCt);
            
        hideCt = StartCoroutine(HideAfterDelay(2f));
    }

    IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
    }
}
