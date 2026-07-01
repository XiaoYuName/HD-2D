using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CharClickInter : MonoBehaviour, IPointerClickHandler
{
    public event Action OnClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
}
