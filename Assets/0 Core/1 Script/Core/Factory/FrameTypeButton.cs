using UnityEngine;
using System;
using UnityEngine.UI;

public class FrameTypeButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] LocTextSwitch locText;
    [SerializeField] Color activeColor;
    [SerializeField] Color normalColor;
    [SerializeField] Image seSignImage;
    public event Action OnClick;

    void Awake()
    {
        button.onClick.AddListener(OnClickEvent);
    }

    void OnClickEvent()
    {
        OnClick?.Invoke();
    }

    public void Set(FactoryFrameType type)
    {
        locText.SetText(LocTableSet.Factory, type.LocKey());
    }

    public void SetSelected(bool selected)
    {
        button.targetGraphic.color = selected ? activeColor : normalColor;
        seSignImage.enabled = selected;
    }
}
