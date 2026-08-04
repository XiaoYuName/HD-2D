using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>送礼面板的礼物格子：图标 + 数量 + 选中态，点击回传自身。</summary>
    public sealed class GiftItemSlot : UIBase, IPointerClickHandler
    {
        Image iconImage;
        TMP_Text countText;
        GameObject selectedMark;
        Action<GiftItemSlot> onClick;

        public ItemInfo Item { get; private set; }

        public override void Init()
        {
            iconImage = Get<Image>("IconImage");
            countText = Get<TMP_Text>("CountText");
            selectedMark = Get("SelectedMark");
        }

        public void SetData(ItemInfo item, Action<GiftItemSlot> click)
        {
            Item = item;
            onClick = click;

            ItemData itemData = InventoryManager.Instance.GetItemData(item.ID);
            if (itemData == null)
            {
                iconImage.ClearIcon();
            }
            else
            {
                iconImage.SetIcon(GamePathTools.CombinationItemIconPath(itemData.IconName));
            }

            countText.text = item.Count > 1 ? $"X{item.Count}" : string.Empty;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectedMark != null)
            {
                selectedMark.SetActive(selected);
            }
        }

        public override void Release()
        {
            if (iconImage != null)
            {
                iconImage.ClearIcon();
            }

            Item = null;
            onClick = null;
            base.Release();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke(this);
        }
    }
}
