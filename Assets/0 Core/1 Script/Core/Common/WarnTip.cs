using TMPro;
using UnityEngine;
using System.Collections;
using UnityEngine.Localization.Components;
using PrimeTween;

public class WarnTip : MonoBehaviour
{
    [SerializeField] LocalizeStringEvent contentText;
    [SerializeField] ShakeSettings ss;
    Coroutine hideCt;
    Sequence seq; 

    void OnDisable()
    {
        Close();
    }
    public void Show(string table, string key)
    {
        contentText.SetText(table, key);
        gameObject.SetActive(true);
        if(hideCt != null)
            StopCoroutine(hideCt);
            
        hideCt = StartCoroutine(HideAfterDelay(2f));

        seq.Stop();
        seq = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.ShakeLocalPosition(transform, ss))
        ;

    }
    public void Close()
    {
        seq.Stop();
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
