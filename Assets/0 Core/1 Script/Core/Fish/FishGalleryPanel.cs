using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

namespace XFramework.Fish
{
    /// <summary>
    /// 钓鱼图鉴面板：展示所有战利品（鱼类 + 钓鱼产品 FishingProduct）。
    /// 左侧为带星级的 Grid 列表；右侧为选中项详情（名称/介绍/最长长度/最重重量），钓鱼产品不展示长度与重量。
    /// 新解锁且未查看过的条目显示 NEW 标记（查看后消除，已读状态随存档持久化在 <see cref="InventoryManager"/> 的物品解锁数据中）。
    /// 鱼类清单以 <see cref="FishConfig"/> 为准，长度/重量取玩家历史最大钓获记录（<see cref="FishGameManager.GetBestCatch"/>）；
    /// 产品以物品表内 MaterialType==FishingProduct 为准。
    /// </summary>
    public class FishGalleryPanel : UIBase
    {
        [LabelText("鱼种配表")][SerializeField] FishConfig config;
        [SerializeField] Button closeButton;
        [LabelText("是否展示未解锁条目(剪影)")][SerializeField] bool showLockedEntries = true;

        [Header("左侧列表")]
        [SerializeField] FishGalleryCellUI cellPrefab;
        [SerializeField] Transform cellContainer;

        [Header("右侧详情")]
        [SerializeField] Image detailIcon;
        [SerializeField] LocalizeStringEvent detailNameText;
        [SerializeField] LocalizeStringEvent detailDescText;
        [LabelText("长度/重量根节点(钓鱼产品隐藏)")][SerializeField] GameObject sizeRoot;
        [SerializeField] TextMeshProUGUI detailLengthText, detailWeightText;
        [LabelText("价格文本(可空)")][SerializeField] TextMeshProUGUI detailPriceText;
        [LabelText("详情 NEW 标记(可空)")][SerializeField] GameObject detailNewIcon;
        [SerializeField] GameObject detailStarPrefab;
        [SerializeField] Transform detailStarContainer;
        [LabelText("已解锁详情根")][SerializeField] GameObject detailUnlockedRoot;
        [LabelText("未解锁详情根(未解锁提示)")][SerializeField] GameObject detailLockedRoot;

        readonly List<FishGalleryCellUI> cells = new();
        readonly List<GameObject> detailStars = new();
        readonly List<FishGalleryEntry> entries = new();
        FishGalleryCellUI selectedCell;

#if UNITY_EDITOR
        [PropertySpace(8)]
        [Button("一键生成图鉴 UI 骨架", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
        void CreateUI()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                Debug.LogError("[FishGalleryPanel] 需挂在带 RectTransform 的 UI 物体上再生成。");
                return;
            }

            // 脚手架：清空已有子物体后重建（请在空面板上运行，勿覆盖已连好的成品）
            for (int i = root.childCount - 1; i >= 0; i--)
                DestroyImmediate(root.GetChild(i).gameObject);
            SetAnchored(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 半透明背景（拦截点击）
            RectTransform bg = CreateRect("Bg", root);
            SetAnchored(bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(bg, new Color(0f, 0f, 0f, 0.6f));

            // 主窗口
            RectTransform window = CreateRect("Window", root);
            SetBox(window, new Vector2(0.5f, 0.5f), new Vector2(1120f, 640f), Vector2.zero);
            AddImage(window, new Color(0.96f, 0.96f, 0.96f, 1f));

            // 关闭按钮
            RectTransform closeRt = CreateRect("CloseButton", window);
            SetBox(closeRt, new Vector2(1f, 1f), new Vector2(64f, 64f), new Vector2(-16f, -16f));
            Image closeImg = AddImage(closeRt, new Color(0.85f, 0.3f, 0.3f, 1f));
            closeButton = closeRt.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImg;

            // 标题
            RectTransform titleRt = CreateRect("Title", window);
            SetAnchored(titleRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -72f), new Vector2(-96f, -20f));
            AddText(titleRt, "钓鱼图鉴", 34f, TextAlignmentOptions.Left);

            // ---- 左侧 Grid 列表（ScrollRect + Grid）----
            RectTransform scroll = CreateRect("GridScroll", window);
            SetAnchored(scroll, new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(40f, 40f), new Vector2(-16f, -88f));
            AddImage(scroll, new Color(1f, 1f, 1f, 0.5f));
            ScrollRect scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            RectTransform viewport = CreateRect("Viewport", scroll);
            SetAnchored(viewport, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            viewport.gameObject.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport;

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150f, 176f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperLeft;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;
            cellContainer = content;

            // ---- 右侧详情 ----
            RectTransform right = CreateRect("Detail", window);
            SetAnchored(right, new Vector2(0.6f, 0f), new Vector2(1f, 1f), new Vector2(16f, 40f), new Vector2(-40f, -88f));
            AddImage(right, new Color(1f, 1f, 1f, 0.35f));

            RectTransform unlockedRoot = CreateRect("DetailUnlockedRoot", right);
            SetAnchored(unlockedRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            detailUnlockedRoot = unlockedRoot.gameObject;

            RectTransform dIcon = CreateRect("DetailIcon", unlockedRoot);
            SetBox(dIcon, new Vector2(0.5f, 1f), new Vector2(260f, 260f), new Vector2(0f, -24f));
            detailIcon = AddImage(dIcon, Color.white);

            RectTransform dNewRt = CreateRect("DetailNewIcon", unlockedRoot);
            SetBox(dNewRt, new Vector2(0f, 1f), new Vector2(64f, 32f), new Vector2(12f, -12f));
            AddImage(dNewRt, new Color(1f, 0.6f, 0.1f, 1f));
            detailNewIcon = dNewRt.gameObject;

            RectTransform nameRt = CreateRect("NameText", unlockedRoot);
            SetAnchored(nameRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -330f), new Vector2(-20f, -292f));
            detailNameText = AddLoc(nameRt, "×× 鱼名", 30f, TextAlignmentOptions.Left);

            RectTransform starRow = CreateRect("StarRow", unlockedRoot);
            SetAnchored(starRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -372f), new Vector2(-20f, -336f));
            HorizontalLayoutGroup starLayout = starRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            starLayout.childAlignment = TextAnchor.MiddleLeft;
            starLayout.spacing = 4f;
            starLayout.childForceExpandWidth = false;
            starLayout.childForceExpandHeight = false;
            detailStarContainer = starRow;

            RectTransform descRt = CreateRect("DescText", unlockedRoot);
            SetAnchored(descRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -480f), new Vector2(-20f, -380f));
            detailDescText = AddLoc(descRt, "xx 鱼介绍，功能介绍，用途介绍……", 22f, TextAlignmentOptions.TopLeft);

            // 长度/重量根（钓鱼产品运行时隐藏）
            RectTransform sizeRt = CreateRect("SizeRoot", unlockedRoot);
            SetAnchored(sizeRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 70f), new Vector2(-20f, 150f));
            sizeRoot = sizeRt.gameObject;
            VerticalLayoutGroup sizeLayout = sizeRt.gameObject.AddComponent<VerticalLayoutGroup>();
            sizeLayout.spacing = 6f;
            sizeLayout.childForceExpandHeight = false;
            RectTransform lenRt = CreateRect("LengthText", sizeRt);
            detailLengthText = AddText(lenRt, "最长长度：-- cm", 22f, TextAlignmentOptions.Left);
            RectTransform wRt = CreateRect("WeightText", sizeRt);
            detailWeightText = AddText(wRt, "最重重量：-- kg", 22f, TextAlignmentOptions.Left);

            RectTransform priceRt = CreateRect("PriceText", unlockedRoot);
            SetAnchored(priceRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 24f), new Vector2(-20f, 58f));
            detailPriceText = AddText(priceRt, "价格：0", 22f, TextAlignmentOptions.Left);

            // 未解锁详情根
            RectTransform lockedRoot = CreateRect("DetailLockedRoot", right);
            SetAnchored(lockedRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            detailLockedRoot = lockedRoot.gameObject;
            RectTransform lockTip = CreateRect("LockedTip", lockedRoot);
            SetAnchored(lockTip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddText(lockTip, "？？？\n尚未捕获", 32f, TextAlignmentOptions.Center);
            lockedRoot.gameObject.SetActive(false);

            // ---- 星星模板（供左侧格子与右侧详情共用 Instantiate）----
            RectTransform starTmpl = CreateRect("StarTemplate", window);
            SetBox(starTmpl, new Vector2(0.5f, 0.5f), new Vector2(24f, 24f), Vector2.zero);
            AddImage(starTmpl, new Color(1f, 0.85f, 0.2f, 1f));
            starTmpl.gameObject.SetActive(false);
            detailStarPrefab = starTmpl.gameObject;

            // ---- 单元格模板 ----
            RectTransform cellRt = CreateRect("CellTemplate", window);
            SetBox(cellRt, new Vector2(0.5f, 0.5f), new Vector2(150f, 176f), Vector2.zero);
            Image cellBg = AddImage(cellRt, new Color(0.9f, 0.9f, 0.9f, 1f));
            FishGalleryCellUI cell = cellRt.gameObject.AddComponent<FishGalleryCellUI>();
            Button cellBtn = cellRt.gameObject.AddComponent<Button>();
            cellBtn.targetGraphic = cellBg;

            RectTransform cIcon = CreateRect("Icon", cellRt);
            SetAnchored(cIcon, Vector2.zero, Vector2.one, new Vector2(12f, 28f), new Vector2(-12f, -12f));
            Image cIconImg = AddImage(cIcon, Color.white);

            RectTransform cSel = CreateRect("SelectedFrame", cellRt);
            SetAnchored(cSel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(cSel, new Color(0.2f, 0.6f, 1f, 0.35f));
            cSel.gameObject.SetActive(false);

            RectTransform cNew = CreateRect("NewIcon", cellRt);
            SetBox(cNew, new Vector2(1f, 1f), new Vector2(48f, 26f), new Vector2(-4f, -4f));
            AddImage(cNew, new Color(1f, 0.6f, 0.1f, 1f));
            cNew.gameObject.SetActive(false);

            RectTransform cLock = CreateRect("LockMask", cellRt);
            SetAnchored(cLock, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(cLock, new Color(0.2f, 0.2f, 0.2f, 0.7f));
            cLock.gameObject.SetActive(false);

            RectTransform cCount = CreateRect("CountText", cellRt);
            SetAnchored(cCount, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 4f), new Vector2(-6f, 26f));
            TextMeshProUGUI cCountText = AddText(cCount, "x0", 20f, TextAlignmentOptions.Right);

            RectTransform cStars = CreateRect("StarContainer", cellRt);
            SetAnchored(cStars, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 26f), new Vector2(-6f, 52f));
            HorizontalLayoutGroup cStarLayout = cStars.gameObject.AddComponent<HorizontalLayoutGroup>();
            cStarLayout.childAlignment = TextAnchor.MiddleCenter;
            cStarLayout.spacing = 2f;
            cStarLayout.childForceExpandWidth = false;
            cStarLayout.childForceExpandHeight = false;

            cell.EditorAutoBind(cellBtn, cIconImg, cSel.gameObject, cNew.gameObject, cLock.gameObject,
                cCountText, starTmpl.gameObject, cStars);
            cellRt.gameObject.SetActive(false);
            cellPrefab = cell;

            // 自动关联工程内的 FishConfig
            if (config == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:FishConfig");
                if (guids.Length > 0)
                    config = AssetDatabase.LoadAssetAtPath<FishConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            EditorUtility.SetDirty(this);
            Debug.Log("[FishGalleryPanel] UI 骨架已生成并自动连线，请替换占位图/字体、按需微调布局后保存。");
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        static void SetAnchored(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        static void SetBox(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static Image AddImage(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        static TextMeshProUGUI AddText(RectTransform rt, string text, float size, TextAlignmentOptions align)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            return t;
        }

        // 生成一个 TMP + LocalizeStringEvent，并把 OnUpdateString 动态绑到 TMP.SetText，
        // 让运行时 SetText(table,key) 能刷新文本（等价于 Localization 组件在编辑器里的自动连线）。
        static LocalizeStringEvent AddLoc(RectTransform rt, string placeholder, float size, TextAlignmentOptions align)
        {
            TextMeshProUGUI tmp = AddText(rt, placeholder, size, align);
            var loc = rt.gameObject.AddComponent<LocalizeStringEvent>();
            UnityEventTools.AddPersistentListener<string>(loc.OnUpdateString, new UnityAction<string>(tmp.SetText));
            return loc;
        }
#endif

        public override void Init()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        public override void Open()
        {
            base.Open();

            BuildEntries();
            BuildCells();

            // 默认选中第一条已解锁的；没有已解锁时选第一条
            FishGalleryCellUI first = cells.Find(c => c.Entry.Unlocked);
            if (first == null && cells.Count > 0)
                first = cells[0];

            if (first != null)
                OnCellClick(first);
            else
                ShowDetail(null);
        }

        public override void Close()
        {
            foreach (FishGalleryCellUI cell in cells)
                Destroy(cell.gameObject);
            cells.Clear();
            entries.Clear();
            selectedCell = null;
            ClearDetailStars();

            base.Close();
        }

        #region 数据构建
        void BuildEntries()
        {
            entries.Clear();
            var fishIds = new HashSet<long>();

            // 鱼类：以 FishConfig 为准（携带长度/重量）
            if (config != null)
            {
                foreach (FishItemData f in config.DataDict.Values)
                {
                    if (!InventoryManager.Instance.HasItemData(f.Id))
                        continue;
                    ItemInfo item = InventoryManager.Instance.NewItem(f.Id, 1);
                    if (item == null)
                        continue;
                    fishIds.Add(f.Id);
                    // 长度/重量展示玩家实际钓获过的历史最大值（未钓获过则为0，详情侧会显示占位符）
                    (float bestLength, float bestWeight) = FishGameManager.Instance.GetBestCatch(f.Id);
                    entries.Add(new FishGalleryEntry
                    {
                        Id = f.Id,
                        Item = item,
                        IsFish = true,
                        MaxLength = bestLength,
                        MaxWeight = bestWeight,
                    });
                }
            }

            // 钓鱼产品：物品表内 MaterialType==FishingProduct（无长度/重量）
            foreach (MaterialItemData m in LubanManager.Instance.TbMaterialItemData.DataList)
            {
                if (m.MaterialType != ItemMaterialType.FishingProduct)
                    continue;
                if (fishIds.Contains(m.ItemID))
                    continue;
                if (!InventoryManager.Instance.HasItemData(m.ItemID))
                    continue;
                ItemInfo item = InventoryManager.Instance.NewItem(m.ItemID, 1);
                if (item == null)
                    continue;
                entries.Add(new FishGalleryEntry
                {
                    Id = m.ItemID,
                    Item = item,
                    IsFish = false,
                });
            }

            // 解锁状态 + NEW 判定
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                FishGalleryEntry e = entries[i];
                e.Unlocked = InventoryManager.Instance.HasItemUnlock(e.Id);
                e.IsNew = e.Unlocked && !InventoryManager.Instance.HasItemSeen(e.Id);
                if (!showLockedEntries && !e.Unlocked)
                    entries.RemoveAt(i);
            }

            // 排序：先按星级从小到大，再按种类（钓鱼产品优先，其次鱼类）
            entries.Sort((a, b) =>
            {
                int qCompare = a.Item.GetQuality().CompareTo(b.Item.GetQuality());
                if (qCompare != 0)
                    return qCompare;
                return a.IsFish.CompareTo(b.IsFish);
            });
        }

        void BuildCells()
        {
            foreach (FishGalleryCellUI cell in cells)
                Destroy(cell.gameObject);
            cells.Clear();

            foreach (FishGalleryEntry e in entries)
            {
                FishGalleryCellUI cell = Instantiate(cellPrefab, cellContainer);
                cell.gameObject.SetActive(true);
                cell.Set(e, OnCellClick);
                cells.Add(cell);
            }
        }
        #endregion

        #region 选中 / 详情
        void OnCellClick(FishGalleryCellUI cell)
        {
            if (selectedCell != null)
                selectedCell.SetSelected(false);
            selectedCell = cell;
            cell.SetSelected(true);

            FishGalleryEntry e = cell.Entry;

            // 查看即视为已读，消除 NEW
            if (e.Unlocked && e.IsNew)
            {
                e.IsNew = false;
                InventoryManager.Instance.MarkItemSeen(e.Id);
                cell.RefreshNew();
            }

            ShowDetail(e);
        }

        void ShowDetail(FishGalleryEntry e)
        {
            bool has = e != null;
            bool unlocked = has && e.Unlocked;

            if (detailUnlockedRoot != null)
                detailUnlockedRoot.SetActive(unlocked);
            if (detailLockedRoot != null)
                detailLockedRoot.SetActive(has && !unlocked);
            if (detailNewIcon != null)
                detailNewIcon.SetActive(false);

            if (!unlocked)
            {
                ClearDetailStars();
                return;
            }

            ItemInfo item = e.Item;
            if (detailIcon != null)
                detailIcon.SetIcon(item.GetIconPath());
            if (detailNameText != null)
                detailNameText.SetText(item.GetNameTable(), item.GetNameKey());
            if (detailDescText != null)
                detailDescText.SetText(item.GetDescTable(), item.GetDescKey());
            if (detailPriceText != null)
                detailPriceText.text = item.GetValue().ToString();

            RebuildDetailStars((int)item.GetQuality());

            // 鱼类展示长度/重量；钓鱼产品隐藏
            if (sizeRoot != null)
                sizeRoot.SetActive(e.IsFish);
            if (e.IsFish)
            {
                // 记录取历史最大钓获值；从未真正钓获过时显示占位符
                bool hasCatch = e.MaxLength > 0f || e.MaxWeight > 0f;
                if (detailLengthText != null)
                    detailLengthText.text = hasCatch ? e.MaxLength.ToString("0.00") + "cm" : "--cm";
                if (detailWeightText != null)
                    detailWeightText.text = hasCatch ? e.MaxWeight.ToString("0.00") + "kg" : "--kg";
            }
        }

        void RebuildDetailStars(int count)
        {
            ClearDetailStars();
            if (detailStarPrefab == null || detailStarContainer == null)
                return;
            for (int i = 0; i < count; i++)
            {
                GameObject star = Instantiate(detailStarPrefab, detailStarContainer);
                star.SetActive(true);
                detailStars.Add(star);
            }
        }

        void ClearDetailStars()
        {
            foreach (GameObject star in detailStars)
                Destroy(star);
            detailStars.Clear();
        }
        #endregion
    }

    /// <summary>钓鱼图鉴单条战利品的运行时数据（鱼类或钓鱼产品）。</summary>
    public class FishGalleryEntry
    {
        public long Id;
        public ItemInfo Item;
        public bool IsFish;      // true=鱼类(展示长度/重量)；false=钓鱼产品
        public bool Unlocked;    // 是否已解锁(已捕获过)
        public bool IsNew;       // 已解锁但尚未在图鉴中查看
        public float MaxLength;  // 最长长度(cm)，仅鱼类
        public float MaxWeight;  // 最重重量(kg)，仅鱼类
    }
}
