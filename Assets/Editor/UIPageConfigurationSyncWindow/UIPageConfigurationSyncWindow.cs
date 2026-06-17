#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    public class UIPageConfigurationSyncWindow : OdinEditorWindow
    {
        [MenuItem("Tools/XFramework/UI配置同步工具")]
        private static void OpenWindow()
        {
            var window = GetWindow<UIPageConfigurationSyncWindow>();
            window.titleContent = new GUIContent("UI配置同步工具");
            window.minSize = new Vector2(720, 520);
            window.Show();
        }

        [Title("UI配置同步工具")]

        [BoxGroup("配置表")]
        [LabelText("PageConfiguration")]
        [Required("请拖入 PageConfiguration 配置表")]
        public PageConfiguration PageConfiguration;

        [BoxGroup("扫描设置")]
        [LabelText("UI Prefab 搜索目录")]
        [FolderPath(RequireExistingPath = true)]
        [ValidateInput(nameof(IsValidAssetFolder), "必须选择 Assets 目录下的文件夹")]
        public string UIPrefabSearchFolder = "Assets/AddressableAssets/Remote/Prefabs/UI";

        [BoxGroup("扫描设置")]
        [LabelText("包含子文件夹")]
        public bool IncludeSubFolders = true;

        [BoxGroup("扫描设置")]
        [LabelText("搜索子物体组件")]
        [Tooltip("如果 UIBase 子类组件不是挂在 Prefab 根节点，而是在子物体上，可以开启")]
        public bool SearchComponentInChildren = false;

        [BoxGroup("匹配规则")]
        [LabelText("优先根据组件匹配")]
        [Tooltip("检查 Prefab 上是否挂载了对应的 UIBase 子类")]
        public bool MatchByComponent = true;

        [BoxGroup("匹配规则")]
        [LabelText("组件找不到时使用类名匹配")]
        [Tooltip("例如 BagUI.cs 对应 BagUI.prefab")]
        public bool MatchByClassName = true;

        [BoxGroup("写入规则")]
        [LabelText("更新已存在配置")]
        [Tooltip("如果 PathID 已经存在，是否更新它的 PagePath")]
        public bool UpdateExistingItem = true;

        [BoxGroup("写入规则")]
        [LabelText("自动添加缺失配置")]
        public bool AddMissingItem = true;

        [BoxGroup("写入规则")]
        [LabelText("同步后按 PathID 排序")]
        public bool SortAfterSync = true;

        [BoxGroup("默认字段")]
        [LabelText("默认适配层级")]
        public UICanvasLayer DefaultCanvasLayer;

        [BoxGroup("默认字段")]
        [LabelText("默认子层级")]
        public UIParentLayer DefaultParentLayer;

        [BoxGroup("默认字段")]
        [LabelText("默认播放 Tween")]
        public bool DefaultTween = false;

        [BoxGroup("预览")]
        [ReadOnly]
        [LabelText("找到的 UIBase 子类数量")]
        public int UIBaseTypeCount;

        [BoxGroup("预览")]
        [ReadOnly]
        [LabelText("找到的 Prefab 数量")]
        public int PrefabCount;

        [BoxGroup("预览")]
        [ReadOnly]
        [LabelText("可匹配数量")]
        public int MatchedCount;

        [BoxGroup("预览")]
        [ReadOnly]
        [LabelText("未匹配数量")]
        public int MissingCount;

        [BoxGroup("预览")]
        [TableList]
        [LabelText("匹配结果")]
        public List<UIMatchPreviewItem> PreviewItems = new List<UIMatchPreviewItem>();

        [BoxGroup("操作")]
        [Button("刷新预览", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.7f, 1f)]
        private void RefreshPreview()
        {
            PreviewItems = BuildPreview();

            UIBaseTypeCount = PreviewItems.Count;
            MatchedCount = PreviewItems.Count(x => x.IsMatched);
            MissingCount = PreviewItems.Count(x => !x.IsMatched);

            Debug.Log($"UI配置预览刷新完成。UIBase: {UIBaseTypeCount}, 匹配: {MatchedCount}, 未匹配: {MissingCount}");
        }

        [BoxGroup("操作")]
        [Button("同步到 PageConfiguration", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.9f, 0.4f)]
        private void SyncToPageConfiguration()
        {
            if (PageConfiguration == null)
            {
                Debug.LogError("请先选择 PageConfiguration 配置表。");
                return;
            }

            if (!IsValidAssetFolder(UIPrefabSearchFolder))
            {
                Debug.LogError($"UI Prefab 搜索目录无效: {UIPrefabSearchFolder}");
                return;
            }

            List<UIMatchPreviewItem> previewItems = BuildPreview();

            if (PageConfiguration.Pages == null)
            {
                PageConfiguration.Pages = new List<UIPageItem>();
            }

            Undo.RecordObject(PageConfiguration, "Sync UI Page Configuration");

            int addCount = 0;
            int updateCount = 0;
            int skipCount = 0;
            int missingCount = 0;

            foreach (UIMatchPreviewItem item in previewItems)
            {
                if (!item.IsMatched)
                {
                    missingCount++;
                    continue;
                }

                UIPageItem existingItem = PageConfiguration.Pages.Find(x => x.PathID == item.PathID);

                if (existingItem != null)
                {
                    if (!UpdateExistingItem)
                    {
                        skipCount++;
                        continue;
                    }

                    existingItem.PagePath = item.PrefabPath;
                    updateCount++;
                    continue;
                }

                if (!AddMissingItem)
                {
                    skipCount++;
                    continue;
                }

                UIPageItem newItem = new UIPageItem
                {
                    PathID = item.PathID,
                    PagePath = item.PrefabPath,
                    UICanvas = DefaultCanvasLayer,
                    UIParent = DefaultParentLayer,
                    isTween = DefaultTween
                };

                PageConfiguration.Pages.Add(newItem);
                addCount++;
            }

            if (SortAfterSync)
            {
                PageConfiguration.Pages = PageConfiguration.Pages
                    .OrderBy(x => x.PathID)
                    .ToList();
            }

            EditorUtility.SetDirty(PageConfiguration);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            PreviewItems = previewItems;

            UIBaseTypeCount = previewItems.Count;
            MatchedCount = previewItems.Count(x => x.IsMatched);
            MissingCount = previewItems.Count(x => !x.IsMatched);

            Debug.Log(
                $"同步完成：\n" +
                $"新增: {addCount}\n" +
                $"更新: {updateCount}\n" +
                $"跳过: {skipCount}\n" +
                $"未匹配Prefab: {missingCount}",
                PageConfiguration);
        }

        [BoxGroup("操作")]
        [Button("检查配置表中缺失的 UIBase", ButtonSizes.Medium)]
        private void CheckMissingUIBaseInConfig()
        {
            if (PageConfiguration == null)
            {
                Debug.LogError("请先选择 PageConfiguration 配置表。");
                return;
            }

            List<Type> uiTypes = GetUIBaseTypes();

            List<string> missing = new List<string>();

            foreach (Type type in uiTypes)
            {
                bool exists = PageConfiguration.Pages != null &&
                              PageConfiguration.Pages.Exists(x => x.PathID == type.Name);

                if (!exists)
                {
                    missing.Add(type.Name);
                }
            }

            if (missing.Count == 0)
            {
                Debug.Log("PageConfiguration 中没有缺失的 UIBase 配置。", PageConfiguration);
            }
            else
            {
                Debug.LogWarning(
                    "以下 UIBase 子类没有加入 PageConfiguration：\n" +
                    string.Join("\n", missing),
                    PageConfiguration);
            }
        }

        [BoxGroup("操作")]
        [Button("清理配置表中无效路径", ButtonSizes.Medium)]
        [GUIColor(1f, 0.75f, 0.3f)]
        private void CheckInvalidPagePaths()
        {
            if (PageConfiguration == null)
            {
                Debug.LogError("请先选择 PageConfiguration 配置表。");
                return;
            }

            if (PageConfiguration.Pages == null || PageConfiguration.Pages.Count == 0)
            {
                Debug.LogWarning("PageConfiguration.Pages 为空。", PageConfiguration);
                return;
            }

            List<string> invalidList = new List<string>();

            foreach (UIPageItem page in PageConfiguration.Pages)
            {
                if (string.IsNullOrEmpty(page.PagePath))
                {
                    invalidList.Add($"{page.PathID} : 路径为空");
                    continue;
                }

                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(page.PagePath);

                if (asset == null)
                {
                    invalidList.Add($"{page.PathID} : {page.PagePath}");
                }
            }

            if (invalidList.Count == 0)
            {
                Debug.Log("没有发现无效 UI 路径。", PageConfiguration);
            }
            else
            {
                Debug.LogWarning(
                    "以下 UI 配置路径无效：\n" +
                    string.Join("\n", invalidList),
                    PageConfiguration);
            }
        }

        private List<UIMatchPreviewItem> BuildPreview()
        {
            List<UIMatchPreviewItem> result = new List<UIMatchPreviewItem>();

            if (!IsValidAssetFolder(UIPrefabSearchFolder))
            {
                Debug.LogError($"UI Prefab 搜索目录无效: {UIPrefabSearchFolder}");
                return result;
            }

            List<Type> uiTypes = GetUIBaseTypes();
            List<string> prefabPaths = FindPrefabPaths(UIPrefabSearchFolder);

            PrefabCount = prefabPaths.Count;

            foreach (Type uiType in uiTypes)
            {
                string prefabPath = FindMatchedPrefabPath(uiType, prefabPaths);

                result.Add(new UIMatchPreviewItem
                {
                    PathID = uiType.Name,
                    UITypeFullName = uiType.FullName,
                    PrefabPath = prefabPath,
                    IsMatched = !string.IsNullOrEmpty(prefabPath)
                });
            }

            result = result
                .OrderByDescending(x => x.IsMatched)
                .ThenBy(x => x.PathID)
                .ToList();

            return result;
        }

        private static List<Type> GetUIBaseTypes()
        {
            return TypeCache.GetTypesDerivedFrom<UIBase>()
                .Where(type =>
                    type != null &&
                    !type.IsAbstract &&
                    !type.IsGenericType &&
                    typeof(MonoBehaviour).IsAssignableFrom(type))
                .OrderBy(type => type.Name)
                .ToList();
        }

        private List<string> FindPrefabPaths(string folder)
        {
            List<string> result = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IncludeSubFolders)
                {
                    string directory = Path.GetDirectoryName(path)?.Replace("\\", "/");

                    if (directory != UIPrefabSearchFolder)
                    {
                        continue;
                    }
                }

                result.Add(path);
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private string FindMatchedPrefabPath(Type uiType, List<string> prefabPaths)
        {
            if (MatchByComponent)
            {
                foreach (string prefabPath in prefabPaths)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                    if (prefab == null)
                    {
                        continue;
                    }

                    Component component = SearchComponentInChildren
                        ? prefab.GetComponentInChildren(uiType, true)
                        : prefab.GetComponent(uiType);

                    if (component != null)
                    {
                        return prefabPath;
                    }
                }
            }

            if (MatchByClassName)
            {
                string typeName = uiType.Name;

                foreach (string prefabPath in prefabPaths)
                {
                    string prefabName = Path.GetFileNameWithoutExtension(prefabPath);

                    if (prefabName == typeName)
                    {
                        return prefabPath;
                    }
                }
            }

            return null;
        }

        private static bool IsValidAssetFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            path = path.Replace("\\", "/");

            if (!path.StartsWith("Assets"))
            {
                return false;
            }

            return AssetDatabase.IsValidFolder(path);
        }
    }

    [Serializable]
    public class UIMatchPreviewItem
    {
        [TableColumnWidth(160)]
        [LabelText("PathID")]
        public string PathID;

        [TableColumnWidth(260)]
        [LabelText("UI类型")]
        public string UITypeFullName;

        [LabelText("Prefab路径")]
        public string PrefabPath;

        [TableColumnWidth(70)]
        [LabelText("已匹配")]
        public bool IsMatched;
    }
}

#endif