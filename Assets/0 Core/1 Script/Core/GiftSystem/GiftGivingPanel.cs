using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>送礼面板：只展示背包中存在于 TbGiftItemData 的物品。</summary>
    public sealed class GiftGivingPanel : UIBase
    {
        const string TitleKey = "GiftGivingPanel_Title";
        const string TitleForKey = "GiftGivingPanel_TitleFor";
        const string GiftListTitleKey = "GiftGivingPanel_GiftListTitle";
        const string DetailTitleKey = "GiftGivingPanel_DetailTitle";
        const string EmptyKey = "GiftGivingPanel_Empty";
        const string SelectPromptKey = "GiftGivingPanel_SelectPrompt";
        const string GoodwillKey = "GiftGivingPanel_Goodwill";
        const string ConfirmKey = "GiftGivingPanel_Confirm";
        const string CharacterNameVar = "CharacterName";

        RectTransform content;
        Button closeButton;
        Button confirmButton;
        TMP_Text targetNameText;
        TMP_Text giftListTitleText;
        TMP_Text detailTitleText;
        TMP_Text emptyTipText;
        TMP_Text itemNameText;
        TMP_Text effectText;
        TMP_Text confirmLabelText;
        GiftResultTip resultTip;
        GameObject emptyTip;

        readonly List<GiftItemSlot> slots = new();
        readonly List<ItemInfo> giftItems = new();
        readonly LocalizedString titleForLoc = new(LocTableSet.GitfSystem, TitleForKey);
        NpcData targetNpc;
        ItemInfo selectedItem;
        GiftItemData selectedGift;
        bool isEventAdded;

        public override void Init()
        {
            content = Get<RectTransform>("Window/GiftScrollView/Viewport/Content");
            closeButton = Get<Button>("Window/CloseButton");
            confirmButton = Get<Button>("Window/ConfirmButton");
            targetNameText = Get<TMP_Text>("Window/TargetNameText");
            giftListTitleText = Get<TMP_Text>("Window/GiftListTitleText");
            detailTitleText = Get<TMP_Text>("Window/DetailTitleText");
            emptyTipText = Get<TMP_Text>("Window/GiftScrollView/Viewport/EmptyTip");
            itemNameText = Get<TMP_Text>("Window/Detail/ItemNameText");
            effectText = Get<TMP_Text>("Window/Detail/EffectText");
            confirmLabelText = Get<TMP_Text>("Window/ConfirmButton/Label");
            resultTip = Get<GiftResultTip>("Window/StatusText");
            emptyTip = Get("Window/GiftScrollView/Viewport/EmptyTip");

            Bind(closeButton, Close, string.Empty);
            Bind(confirmButton, GiveSelectedGift, string.Empty);
            ResetSelection();
        }

        public override void Open()
        {
            base.Open();
            AddEvents();
            RefreshLocalization();
        }

        public override void Close()
        {
            RemoveEvents();
            targetNpc = null;
            resultTip.Clear();
            ClearSlots();
            base.Close();
        }

        protected override void OnDestroy()
        {
            RemoveEvents();
            base.OnDestroy();
        }

        void AddEvents()
        {
            if (isEventAdded)
                return;

            InventoryManager.Instance.RegisterAllItemChange(OnInventoryChanged);
            LanguageManager.Instance.AddOnLanguageChanged(RefreshLocalization);
            isEventAdded = true;
        }

        void RemoveEvents()
        {
            if (!isEventAdded)
                return;

            InventoryManager.Instance.UnregisterAllItemChange(OnInventoryChanged);
            LanguageManager.Instance.RemoveOnLanguageChanged(RefreshLocalization);
            isEventAdded = false;
        }

        public void Show(NpcData npcData)
        {
            targetNpc = npcData;
            resultTip.Clear();
            ResetSelection();
            RefreshLocalization();
        }

        void OnInventoryChanged(List<ItemInfo> items)
        {
            giftItems.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                ItemInfo item = items[i];
                if (LubanManager.Instance.TbGiftItemData.GetOrDefault(item.ID) != null)
                {
                    giftItems.Add(item);
                }
            }

            BuildSlots();
        }

        void BuildSlots()
        {
            ClearSlots();
            emptyTip.SetActive(giftItems.Count == 0);
            for (int i = 0; i < giftItems.Count; i++)
            {
                GameObject slotObject = AssetsManager.Instance.Instantiate(AssetKeys.GiftItemSlotPath);
                RectTransform slotRect = slotObject.GetComponent<RectTransform>();
                slotRect.SetParent(content, false);
                // 对象池 Free 时是带世界坐标换父节点的，回池一次缩放就被 Canvas 缩放系数乘一次，
                // 复用出来会越来越小直到看不见，所以取出后必须把 localScale/旋转归位。
                slotRect.localScale = Vector3.one;
                slotRect.localRotation = Quaternion.identity;
                GiftItemSlot slot = slotObject.GetComponent<GiftItemSlot>();
                slot.SetData(giftItems[i], OnSlotClicked);
                slots.Add(slot);
            }

            RestoreSelection();
        }

        /// <summary>重建列表后沿用原来的选中：同一堆还在就继续选它，堆被扣空了顺延到同 ID 的另一堆，都没了才清空。</summary>
        void RestoreSelection()
        {
            GiftItemSlot target = FindSlot(selectedItem);
            if (target == null)
            {
                ResetSelection();
                return;
            }

            ApplySelection(target);
        }

        GiftItemSlot FindSlot(ItemInfo item)
        {
            if (item == null)
            {
                return null;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Item == item)
                {
                    return slots[i];
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Item != null && slots[i].Item.ID == item.ID)
                {
                    return slots[i];
                }
            }

            return null;
        }

        void OnSlotClicked(GiftItemSlot selectedSlot)
        {
            resultTip.Clear();
            ApplySelection(selectedSlot);
        }

        void ApplySelection(GiftItemSlot selectedSlot)
        {
            selectedItem = selectedSlot == null ? null : selectedSlot.Item;
            selectedGift = selectedItem == null
                ? null
                : LubanManager.Instance.TbGiftItemData.GetOrDefault(selectedItem.ID);

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].SetSelected(slots[i] == selectedSlot);
            }

            if (selectedItem == null || selectedGift == null)
            {
                ResetSelection();
                return;
            }

            RefreshSelectionText();
            confirmButton.interactable = targetNpc != null;
        }

        string BuildEffectText(GiftItemData giftData)
        {
            StringBuilder builder = new();
            builder.Append(GetLoc(GoodwillKey)).Append(' ').Append(FormatSigned(giftData.Goodwill));
            if (targetNpc == null || targetNpc.CharacterData != CharaIdSet1.Machi)
            {
                return builder.ToString();
            }

            for (int i = 0; i < giftData.RewardProp.Count; i++)
            {
                TbRewardPropData reward = giftData.RewardProp[i];
                builder.AppendLine();
                builder.Append(GetPropertyName(reward.PropType)).Append(' ').Append(FormatSigned(reward.Value));
            }

            return builder.ToString();
        }

        void RefreshLocalization()
        {
            giftListTitleText.text = GetLoc(GiftListTitleKey);
            detailTitleText.text = GetLoc(DetailTitleKey);
            emptyTipText.text = GetLoc(EmptyKey);
            confirmLabelText.text = GetLoc(ConfirmKey);
            targetNameText.text = GetTargetTitle();

            if (selectedGift == null)
            {
                itemNameText.text = GetLoc(SelectPromptKey);
            }
            else
            {
                RefreshSelectionText();
            }

            resultTip.RefreshLocalization();
        }

        string GetTargetTitle()
        {
            if (targetNpc == null)
            {
                return GetLoc(TitleKey);
            }

            titleForLoc.SetVar(
                CharacterNameVar,
                LanguageManager.Instance.GetLocalizedString(targetNpc.Name),
                false);
            return titleForLoc.GetLocalizedString();
        }

        void RefreshSelectionText()
        {
            ItemData itemData = InventoryManager.Instance.GetItemData(selectedItem.ID);
            itemNameText.text = itemData == null
                ? selectedGift.Remark
                : LanguageManager.Instance.GetLocalizedString(itemData.NameKey);
            effectText.text = BuildEffectText(selectedGift);
        }

        static string GetLoc(string key) =>
            LanguageManager.Instance.GetLocalizedString(LocTableSet.GitfSystem, key);

        static string GetPropertyName(PropertyType propertyType)
        {
            string machiRoomKey = propertyType switch
            {
                PropertyType.MachiInspire => "MachiRoom/Inspiration",
                PropertyType.MachiPressure => "MachiRoom/Pressure",
                _ => null,
            };
            if (machiRoomKey != null)
            {
                return LanguageManager.Instance.GetLocalizedString(LocTableSet.MachiRoom, machiRoomKey);
            }

            PropertyData propertyData = GameDataManager.Instance.GetPropertyData(propertyType);
            return propertyData?.Name == null
                ? propertyType.ToString()
                : LanguageManager.Instance.GetLocalizedString(propertyData.Name);
        }

        static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();

        void GiveSelectedGift()
        {
            if (targetNpc == null || selectedItem == null)
            {
                return;
            }

            string giftName = itemNameText.text;
            bool success = GiftManager.Instance.TryGiveGift(
                targetNpc.CharacterData,
                selectedItem,
                out GiftItemData giftData);
            if (success)
            {
                resultTip.Show(giftName, giftData);
            }
            else
            {
                resultTip.ShowFailure();
            }
        }

        void ResetSelection()
        {
            selectedItem = null;
            selectedGift = null;
            itemNameText.text = GetLoc(SelectPromptKey);
            effectText.text = string.Empty;
            confirmButton.interactable = false;
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].SetSelected(false);
            }
        }

        void ClearSlots()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                GiftItemSlot slot = slots[i];
                slot.Release();
                AssetsManager.Instance.FreeGameObject(slot.gameObject);
            }

            slots.Clear();
        }
    }
}