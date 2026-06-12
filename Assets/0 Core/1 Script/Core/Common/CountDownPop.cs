using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CountDownPop : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI countDownText;

    void Awake()
    {
        
    }

    public void SetText(int seconds)
    {
        countDownText.text = $"{seconds} s";
    }
}
