using UnityEngine;
using UnityEngine.UI;
using System;

public class PaintSprayGunButton : MonoBehaviour
{
    [SerializeField] Image seBg;
    [SerializeField] Image pigmentIcon;
    [SerializeField] Button button;
    [SerializeField] int index;

    Action<int> onClick;

    void Awake()
    {
        button.onClick.AddListener(OnClickInvoke);
    }
    public void SetData(int value, Color color, Action<int> clickCallback)
    {
        index = value;
        onClick = clickCallback;
        pigmentIcon.color = color;
    }

    void OnClickInvoke()
    {
        onClick?.Invoke(index);
    }

    public void SwitchState(bool isSe)
    {
        seBg.enabled = isSe;
    }

    void OnDestroy()
    {
        button.onClick.RemoveListener(OnClickInvoke);
    }
}
