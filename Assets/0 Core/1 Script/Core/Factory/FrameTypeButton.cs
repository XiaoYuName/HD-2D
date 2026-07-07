using UnityEngine;
using System;
using UnityEngine.UI;

public class FrameTypeButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] LocTextSwitch locText;
    [SerializeField] Color activeColor = new (1f, 0.78f, 0.42f, 1f);
    [SerializeField] Color normalColor = new (0.86f, 0.86f, 0.88f, 1f);
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
    }
}
