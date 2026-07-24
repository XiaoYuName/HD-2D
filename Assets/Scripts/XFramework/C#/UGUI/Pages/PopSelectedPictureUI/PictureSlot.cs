using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace XFramework
{
    public class PictureSlot : UIBase,IPointerClickHandler
    {
        private RawImage image;
        private LocalizeStringEvent itemName;
        private TextMeshProUGUI itemCount;
        private GameObject SelectedMask;

        private ItemData itemData;
        public ItemInfo ItemInfo { get; private set; }

        public Action OnClick;
        
        public override void Release()
        {
            if (itemData != null)
            {
                AssetsManager.Instance.FreeAsset(GamePathTools.CombinationItemIconPath(itemData.IconName));
                itemData = null;
            }

            base.Release();
        }

        /// <summary>
        /// 初始化方法,一般不需要手动调用
        /// </summary>
        public override void Init()
        {
            image = Get<RawImage>("RectMask/RawImage");
            itemName = Get<LocalizeStringEvent>("NameString/PictureNameTex");
            itemCount = Get<TextMeshProUGUI>("NameString/PictureCountTex");
            SelectedMask = Get("SelectedMask");
        }

        public void SetSelected(bool selected)
        {
            SelectedMask.gameObject.SetActive(selected);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick?.Invoke();
        }

        public void InitData(ItemInfo itemInfo)
        {
            this.ItemInfo = itemInfo;
            itemData = InventoryManager.Instance.GetItemData(itemInfo.ID);
            if (itemData == null)
            {
                gameObject.SetActive(false);
                return;
            }
            
            image.texture =
                AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationItemIconPath(itemData.IconName)).texture;
            itemName.SetText(itemData.NameKey.Table,itemData.NameKey.Value);
            itemCount.text = $"X{itemInfo.Count}";
        }

 
    }
}

