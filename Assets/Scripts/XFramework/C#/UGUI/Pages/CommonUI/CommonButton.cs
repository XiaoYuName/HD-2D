using System;
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.EventSystems;

public class CommonButton : AGVButton
{
    private UIEffect _uiEffect;

    private void Start()
    {
        _uiEffect = GetComponent<UIEffect>();
    }


    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        _uiEffect.edgeMode = EdgeMode.Plain;
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        _uiEffect.edgeMode = EdgeMode.None;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            _uiEffect.edgeMode = EdgeMode.None;
        }
    }
}
