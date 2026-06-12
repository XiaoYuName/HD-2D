using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

public class CharInterPanelButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TextMeshProUGUI nameText;

    public Action OnClick;

    // public void Init(string buttonName)
    // {
    //     nameText.text = buttonName;
    // }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
}
