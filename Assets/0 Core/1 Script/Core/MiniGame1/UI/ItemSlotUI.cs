using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TextMeshProUGUI nameText, countText;
    [SerializeField] Image iconBg, iconImage;
    [SerializeField] int index;
    [SerializeReference] ItemInfo info;

    public ItemInfo Info => info;
    public event Action<int> OnClick;

    public void Init(ItemInfo info)
    {
        this.info = info;
        if(info == null)
        {
            nameText.text = string.Empty;
            countText.text = string.Empty;
            iconImage.enabled = false;
            iconImage.sprite = null;
            iconBg.enabled = false;
            return;
        }

        nameText.text = info.Name;
        countText.text = info.Count.ToString();
        iconBg.enabled = true;
        iconImage.enabled = true;
        iconImage.sprite = info.Icon;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke(index);
    }
}
