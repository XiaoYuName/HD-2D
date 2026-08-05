using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>送礼面板的礼物格子：图标 + 数量 + 选中态，点击回传自身。</summary>
    public sealed class GiftItemSlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text countText;
        [SerializeField] GameObject selectedMark;
        Action<GiftItemSlot> onClick;

        public ItemInfo Item { get; private set; }

        public void SetData(ItemInfo item, Action<GiftItemSlot> click)
        {
            Item = item;
            onClick = click;

            ItemData itemData = InventoryManager.Instance.GetItemData(item.ID);
            iconImage.SetIcon(GamePathTools.CombinationItemIconPath(itemData.IconName));
            countText.text = item.Count > 1 ? $"X{item.Count}" : string.Empty;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            selectedMark.SetActive(selected);
        }

        public void Release()
        {
            iconImage.ClearIcon();
            Item = null;
            onClick = null;
            SetSelected(false);
        }

        void OnDestroy()
        {
            iconImage.ClearIcon();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke(this);
        }
    }
}
