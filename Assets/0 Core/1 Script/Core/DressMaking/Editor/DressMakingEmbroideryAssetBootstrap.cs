#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 首次导入功能时创建可直接运行的 1001/1002 配置，并把运行时入口加入 Remote Group。
/// 已存在的资产不会被覆盖；关卡内容仍由刺绣工作台维护。
/// </summary>
[InitializeOnLoad]
internal static class DressMakingEmbroideryAssetBootstrap
{
    static DressMakingEmbroideryAssetBootstrap()
    {
        EditorApplication.delayCall += EnsureAssets;
    }

    static void EnsureAssets()
    {
        EditorApplication.delayCall -= EnsureAssets;

        DressMakingEmbroiderySimulationGameConfig config =
            AssetDatabase.LoadAssetAtPath<DressMakingEmbroiderySimulationGameConfig>(
                DressMakingEmbroiderySimulationGameConfig.DefaultConfigPath);
        if (config == null)
        {
            string absoluteFolder = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                Path.GetDirectoryName(DressMakingEmbroiderySimulationGameConfig.DefaultConfigPath) ?? string.Empty);
            Directory.CreateDirectory(absoluteFolder);

            config = ScriptableObject.CreateInstance<DressMakingEmbroiderySimulationGameConfig>();
            config.EnsureSampleData();
            config.Normalize();
            AssetDatabase.CreateAsset(config, DressMakingEmbroiderySimulationGameConfig.DefaultConfigPath);
        }

        bool upgraded = config.UpgradeSchema();
        if (upgraded)
        {
            // 旧关卡只导入可编辑线网，不自动重建单元。
            // 自动重建可能因旧版贝塞尔采样误差产生狭长碎片；由工作台显式生成后再覆盖。
            foreach (DressMakingEmbroideryLevelData level in config.DataDict.Values)
            {
                if (level != null && level.GridLines.Count == 0 && level.Regions.Count > 0)
                    DressMakingEmbroideryGridTopology.ImportLinesFromRegions(level);
            }
            EditorUtility.SetDirty(config);
        }

        EnsureLevelPrefabs(config);
        RegisterAddressable(DressMakingEmbroiderySimulationGameConfig.DefaultConfigPath);
        RegisterAddressable(
            "Assets/AddressableAssets/Remote/Prefabs/UGUI/DressMaking/"
            + "DressMakingEmbroiderySimulationGamePanel.prefab");
        AssetDatabase.SaveAssets();
    }

    static void EnsureLevelPrefabs(DressMakingEmbroiderySimulationGameConfig config)
    {
        if (config?.DataDict == null)
            return;

        const string folder = "Assets/AddressableAssets/Remote/Prefabs/UGUI/DressMaking/Embroidery";
        string absoluteFolder = Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
            folder);
        Directory.CreateDirectory(absoluteFolder);

        bool changed = false;
        foreach (DressMakingEmbroideryLevelData level in config.DataDict.Values)
        {
            if (level == null)
                continue;

            string path = string.IsNullOrEmpty(level.levelPrefabPath)
                ? $"{folder}/EmbroideryLevel_{level.clothingId}.prefab"
                : level.levelPrefabPath;
            GameObject prefab = DressMakingEmbroideryLevelPrefabBuilder.CreateOrUpdate(level, path);

            if (prefab == null)
                continue;

            if (level.levelPrefab != prefab || level.levelPrefabPath != path)
            {
                level.levelPrefab = prefab;
                level.levelPrefabPath = path;
                changed = true;
            }

            RegisterAddressable(path);
        }

        if (changed)
        {
            config.Normalize();
            EditorUtility.SetDirty(config);
        }
    }

    static void RegisterAddressable(string assetPath)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
            return;

        AddressableAssetGroup group = settings.FindGroup("Remote") ?? settings.DefaultGroup;
        if (group == null)
            return;

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        if (entry == null || entry.address == assetPath)
            return;

        entry.address = assetPath;
        settings.SetDirty(
            AddressableAssetSettings.ModificationEvent.EntryModified,
            entry,
            true,
            true);
    }
}
#endif
