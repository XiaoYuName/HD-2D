using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMcp
{
    /// <summary>
    /// 统一封装一次读写的目标来源，屏蔽三种 targetMode 的差异：
    /// prefabAsset（LoadPrefabContents 的离屏副本，SaveAsPrefabAsset 落盘 + 可备份）、
    /// prefabStage（当前打开的 Prefab 编辑态，改后标脏由用户保存）、
    /// openScene（当前场景里某个根物体子树，改后标脏由用户保存）。
    /// objectId/hierarchyPath 一律相对 <see cref="RootTf"/> 解析，与来源无关。
    /// </summary>
    sealed class EditTarget : IDisposable
    {
        /// <summary>targetMode 词表的唯一来源（首项即默认值），工具表的 enum 直接引用。</summary>
        public static readonly string[] SupportedModes = { "prefabAsset", "prefabStage", "openScene" };
        public static readonly string[] SupportedRootTransformTypes = { "Transform", "RectTransform" };

        public Transform RootTf { get; private set; }

        /// <summary>true=离屏 Prefab 副本（可落盘、可备份、支持内存预演）；false=实时 stage/场景对象。</summary>
        public bool IsAsset { get; private set; }

        /// <summary>写入白名单校验用的路径（Prefab 资产路径或场景/stage 的资产路径）。</summary>
        public string PolicyPath { get; private set; }

        public bool IsNewAsset { get; private set; }

        string assetPath;          // prefabAsset：落盘/备份用
        GameObject loadedContents; // prefabAsset：需要 Unload
        Scene previewScene;        // 新建 prefabAsset：隔离临时根，避免弄脏当前场景
        Scene dirtyScene;          // prefabStage/openScene：标脏用

        public static bool TryResolve(TargetRequest command, out EditTarget editTarget, out string error)
        {
            editTarget = null;
            error = null;
            switch (string.IsNullOrEmpty(command.targetMode) ? SupportedModes[0] : command.targetMode.Trim())
            {
                case "prefabAsset":
                    if (!PrefabAddress.TryNormalizePrefabPath(command.prefabPath,
                            out string prefabPath, out error))
                        return false;
                    GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefabAsset == null)
                    {
                        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null)
                        {
                            error = "目标路径已存在但不是 Prefab: " + prefabPath;
                            return false;
                        }
                        if (!(command is EditRequest editRequest) || editRequest.createIfMissing == null)
                        {
                            error = "找不到 Prefab: " + prefabPath +
                                "；需要新建时请传 createIfMissing";
                            return false;
                        }
                        return CreateMissing(prefabPath, editRequest.createIfMissing,
                            out editTarget, out error);
                    }
                    editTarget = new EditTarget
                    {
                        loadedContents = PrefabUtility.LoadPrefabContents(prefabPath),
                        IsAsset = true,
                        assetPath = prefabPath,
                        PolicyPath = prefabPath,
                    };
                    editTarget.RootTf = editTarget.loadedContents.transform;
                    return true;

                case "prefabStage":
                {
                    PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage == null)
                    {
                        error = "当前没有打开 Prefab 编辑模式（双击进入某个 Prefab 后再试）";
                        return false;
                    }
                    editTarget = new EditTarget
                    {
                        RootTf = stage.prefabContentsRoot.transform,
                        dirtyScene = stage.scene,
                        PolicyPath = stage.assetPath,
                    };
                    return true;
                }

                case "openScene":
                {
                    if (string.IsNullOrWhiteSpace(command.sceneRootName))
                    {
                        error = "targetMode=openScene 需要 sceneRootName（场景里某个根物体的名字）";
                        return false;
                    }
                    Scene scene = SceneManager.GetActiveScene();
                    GameObject rootGo = scene.GetRootGameObjects().FirstOrDefault(go => go.name == command.sceneRootName);
                    if (rootGo == null)
                    {
                        error = $"当前打开的场景里找不到根物体 {command.sceneRootName}";
                        return false;
                    }
                    editTarget = new EditTarget
                    {
                        RootTf = rootGo.transform,
                        dirtyScene = scene,
                        PolicyPath = scene.path,
                    };
                    return true;
                }

                default:
                    error = $"未知 targetMode: {command.targetMode}（可选 {string.Join("/", SupportedModes)}）";
                    return false;
            }
        }

        static bool CreateMissing(string prefabPath, CreatePrefabOptions options,
            out EditTarget editTarget, out string error)
        {
            editTarget = null;
            string transformType = string.IsNullOrWhiteSpace(options.transformType)
                ? SupportedRootTransformTypes[0]
                : options.transformType.Trim();
            if (!SupportedRootTransformTypes.Any(item =>
                    string.Equals(item, transformType, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"createIfMissing.transformType 无效: {options.transformType}；可选 " +
                    string.Join("/", SupportedRootTransformTypes);
                return false;
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            string rootName = string.IsNullOrWhiteSpace(options.rootName)
                ? Path.GetFileNameWithoutExtension(prefabPath)
                : options.rootName.Trim();
            GameObject rootGo = string.Equals(transformType, "RectTransform", StringComparison.OrdinalIgnoreCase)
                ? new GameObject(rootName, typeof(RectTransform))
                : new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(rootGo, previewScene);
            editTarget = new EditTarget
            {
                RootTf = rootGo.transform,
                loadedContents = rootGo,
                previewScene = previewScene,
                IsAsset = true,
                IsNewAsset = true,
                assetPath = prefabPath,
                PolicyPath = prefabPath,
            };
            error = null;
            return true;
        }

        /// <summary>改动对象前的写入策略校验：总开关 + 目录白名单。返回错误信息或 null。</summary>
        public string ValidateWriteAllowed()
        {
            PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
            if (!settings.AllowPrefabWrites)
                return "写入已被项目配置禁止。请在 Project Settings > Unity Prefab MCP 中启用；" +
                    $"配置资产: {PrefabMcpSettings.AssetPath}";
            if (!string.IsNullOrEmpty(PolicyPath) && !settings.IsPrefabWritePathAllowed(PolicyPath))
                return "目标不在允许写入的目录中: " + PolicyPath;
            return null;
        }

        /// <summary>先备份再持久化（实时目标只标脏、不备份）。返回错误信息或 null。</summary>
        public string Commit(out string backupPath)
        {
            backupPath = IsAsset && !IsNewAsset
                ? PrefabMcpSettings.GetOrCreate().CreatePrefabBackup(assetPath)
                : null;
            if (IsAsset)
            {
                if (IsNewAsset)
                {
                    string folderError = CreateAssetFolders(Path.GetDirectoryName(assetPath));
                    if (folderError != null)
                        return folderError;
                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                        return "保存前目标路径已被其他操作占用: " + assetPath;
                }
                if (PrefabUtility.SaveAsPrefabAsset(loadedContents, assetPath) == null)
                    return "Prefab 保存失败，Unity 未返回已保存资源";
                AssetDatabase.SaveAssets();
                return null;
            }
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            return null;
        }

        static string CreateAssetFolders(string folderPath)
        {
            string normalized = PrefabAddress.NormalizeSlashes(folderPath);
            if (string.IsNullOrEmpty(normalized) || !normalized.StartsWith("Assets", StringComparison.Ordinal))
                return "Prefab 父目录必须位于 Assets 下: " + folderPath;
            if (AssetDatabase.IsValidFolder(normalized))
                return null;

            string current = "Assets";
            string[] parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrEmpty(guid))
                        return "创建 Prefab 父目录失败: " + next;
                }
                current = next;
            }
            return null;
        }

        public void Dispose()
        {
            if (IsNewAsset && previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(previewScene);
            else if (loadedContents != null)
                PrefabUtility.UnloadPrefabContents(loadedContents);
            loadedContents = null;
        }
    }
}
