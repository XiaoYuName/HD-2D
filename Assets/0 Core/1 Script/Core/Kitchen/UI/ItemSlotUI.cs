using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;

namespace XFramework
{
    public class ItemSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] TextMeshProUGUI nameText, countText;
        [SerializeField] LocalizeStringEvent nameLse;

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
                iconImage.ClearIcon();
                iconBg.enabled = false;
                return;
            }

            nameLse.SetText(info.GetNameTable(), info.GetNameKey());
            countText.text = info.Count.ToString();
            iconBg.enabled = true;
            iconImage.enabled = true;
            iconImage.SetIcon(info.GetIconPath());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick?.Invoke(index);
        }
    }

}
