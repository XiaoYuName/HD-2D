using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;
using UnityEngine.Localization.Components;
namespace XFramework
{
    public class ItemSeUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] TextMeshProUGUI nameText, countText;
        [SerializeField] LocalizeStringEvent nameLse;
        [SerializeField] Image iconImage;
        [SerializeField] Image seImage;
        [SerializeField] State state;
        [SerializeReference] ItemInfo info;
        Action<ItemInfo> OnClick;

        public ItemInfo Info => info;
        public State CurState => state;

        public void Init(ItemInfo info, Action<ItemInfo> OnClick)
        {
            this.OnClick = OnClick;
            this.info = info;
            
            if(info != null)
            {
                nameLse.SetText(LocTableSet.InventoryItem, info.GetNameKey());
                // nameText.text = info.Name;
                countText.text = info.Count.ToString();
                iconImage.SetIcon(GamePathTools.CombinationItemIconPath(info.GetIconName()));
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick?.Invoke(info);
        }
        public void SwitchState(State targetState)
        {
            state = targetState;
            if(targetState == State.Se)
            {
                seImage.enabled = true;
            }
            else
            {
                seImage.enabled = false;
            }
        }

        public enum State
        {
            None,
            Se,
        }
    }
}