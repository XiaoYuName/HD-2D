using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework.Fish
{
    /// <summary>
    /// 钓鱼图鉴左侧列表的单个格子：展示一条战利品（鱼类或钓鱼产品）的图标、星级、NEW 标记、解锁状态与拥有数量。
    /// 数据由 <see cref="FishGalleryPanel"/> 通过 <see cref="Set"/> 注入，点击时回传自身。
    /// </summary>
    public class FishGalleryCellUI : MonoBehaviour
    {
        [SerializeField] Button selectButton;
        [SerializeField] Image icon;
        [LabelText("选中高亮框")][SerializeField] GameObject selectedFrame;
        [LabelText("NEW 新获得标记")][SerializeField] GameObject newIcon;
        [LabelText("未解锁遮罩(剪影/问号)")][SerializeField] GameObject lockMask;
        [LabelText("拥有数量文本(可空)")][SerializeField] TextMeshProUGUI countText;
        [SerializeField] GameObject starPrefab;
        [SerializeField] Transform starContainer;
        [SerializeField] List<GameObject> starList = new();

        FishGalleryEntry entry;
        Action<FishGalleryCellUI> onClick;

        public FishGalleryEntry Entry => entry;

        void Awake()
        {
            if (selectButton != null)
                selectButton.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void Set(FishGalleryEntry e, Action<FishGalleryCellUI> clickCb)
        {
            entry = e;
            onClick = clickCb;

            bool unlocked = e.Unlocked;

            // 图标：解锁后显示真实图标；未解锁隐藏，由 lockMask 呈现剪影/问号
            if (icon != null)
            {
                if (unlocked)
                    icon.SetIcon(e.Item.GetIconPath());
                else
                    icon.ClearIcon();
                icon.enabled = unlocked;
            }
            if (lockMask != null)
                lockMask.SetActive(!unlocked);

            // 星级（品质）仅解锁后展示
            RebuildStars(unlocked ? (int)e.Item.GetQuality() : 0);

            // 拥有数量（未解锁或为 0 时隐藏）
            if (countText != null)
            {
                int count = unlocked ? InventoryManager.Instance.GetItemCount(e.Id) : 0;
                countText.gameObject.SetActive(count > 0);
                countText.text = "x" + count;
            }

            RefreshNew();
            SetSelected(false);
        }

        public void RefreshNew()
        {
            if (newIcon != null)
                newIcon.SetActive(entry != null && entry.IsNew);
        }

        public void SetSelected(bool on)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(on);
        }

        void RebuildStars(int count)
        {
            foreach (GameObject star in starList)
                Destroy(star);
            starList.Clear();

            if (starPrefab == null || starContainer == null)
                return;
            for (int i = 0; i < count; i++)
            {
                GameObject star = Instantiate(starPrefab, starContainer);
                star.SetActive(true);
                starList.Add(star);
            }
        }

#if UNITY_EDITOR
        /// <summary>编辑器脚手架用：由 FishGalleryPanel.CreateUI 一键生成时回填各引用。</summary>
        public void EditorAutoBind(Button selectButton, Image icon, GameObject selectedFrame,
            GameObject newIcon, GameObject lockMask, TextMeshProUGUI countText,
            GameObject starPrefab, Transform starContainer)
        {
            this.selectButton = selectButton;
            this.icon = icon;
            this.selectedFrame = selectedFrame;
            this.newIcon = newIcon;
            this.lockMask = lockMask;
            this.countText = countText;
            this.starPrefab = starPrefab;
            this.starContainer = starContainer;
        }
#endif
    }
}
