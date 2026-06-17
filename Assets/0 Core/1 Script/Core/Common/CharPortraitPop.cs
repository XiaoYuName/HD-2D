using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CharPortraitPop : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI contextText;

    public void SetName(string name, Sprite icon)
    {
        nameText.text = name;
        iconImage.sprite = icon;
    }
    public void SetContext(string context)
    {
        contextText.text = context;
    }
}
