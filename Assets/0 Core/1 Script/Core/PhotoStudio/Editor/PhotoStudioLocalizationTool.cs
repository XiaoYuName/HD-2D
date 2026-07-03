using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;

// 拍照系统本地化工具：把各质量档位的「档位名图(源)」(PhotoQualityTierConfig.tierNameSprite)
// 同步进本地化资源表（Asset Table）LocalizeTableSet.PhotoStudioSprite，以档位名(name)为 key。
//
// 运行时 EndPhotoPanel 按当前语言从该资源表取图。各语言图未配齐前，本工具会把同一张源图
// 写进所有语言，先保证显示；之后在 Localization Tables 窗口里逐语言替换为对应翻译图即可，
// 无需改配置面板，也无需手改本地化库文件。
public static class PhotoStudioLocalizationTool
{
    // 资源表集合存放目录（与项目其它本地化表保持一致）
    const string AssetTableDir = "Assets/AddressableAssets/Local/LocalizationTable/AssetsTable/PhotoStudioSprite";

    [MenuItem("Tools/PhotoStudio/同步质量档位精灵到本地化库")]
    public static void SyncQualityTierSprites()
    {
        PhotoStudioGameConfig config = FindConfig();
        if(config == null)
        {
            EditorUtility.DisplayDialog("同步失败", "未找到 PhotoStudioGameConfig 资源。", "确定");
            return;
        }

        if(config.QualityTiers == null || config.QualityTiers.Length == 0)
        {
            EditorUtility.DisplayDialog("同步失败", "PhotoStudioGameConfig 未配置任何质量档位。", "确定");
            return;
        }

        // 取/建资源表集合（不存在时按项目所有语言新建）
        string tableName = LocTableSet.PhotoStudioSprite;
        AssetTableCollection collection = LocalizationEditorSettings.GetAssetTableCollection(tableName);
        if(collection == null)
        {
            EnsureFolder(AssetTableDir);
            collection = LocalizationEditorSettings.CreateAssetTableCollection(tableName, AssetTableDir);
            if(collection == null)
            {
                EditorUtility.DisplayDialog("同步失败", $"创建资源表集合「{tableName}」失败。", "确定");
                return;
            }
        }

        var locales = LocalizationEditorSettings.GetLocales();
        if(locales == null || locales.Count == 0)
        {
            EditorUtility.DisplayDialog("同步失败", "项目未配置任何语言（Locale）。", "确定");
            return;
        }

        int tierCount = 0;
        int skipped = 0;
        foreach(PhotoQualityTierConfig tier in config.QualityTiers)
        {
            if(tier == null || string.IsNullOrEmpty(tier.name))
                continue;
            if(tier.tierNameSprite == null)
            {
                Debug.LogWarning($"[PhotoStudioLocalizationTool] 档位「{tier.name}」未配置「档位名图(源)」，已跳过。");
                skipped++;
                continue;
            }

            // 各语言暂时都写入同一张源图；后续在 Localization 窗口里逐语言替换
            foreach(Locale locale in locales)
                collection.AddAssetToTable(locale.Identifier, tier.name, tier.tierNameSprite);
            tierCount++;
        }

        EditorUtility.SetDirty(collection);
        if(collection.SharedData != null)
            EditorUtility.SetDirty(collection.SharedData);
        foreach(var table in collection.AssetTables)
            EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PhotoStudioLocalizationTool] 已把 {tierCount} 个档位精灵同步进资源表「{tableName}」（{locales.Count} 种语言），跳过 {skipped} 个。");
        EditorUtility.DisplayDialog("同步完成",
            $"已同步 {tierCount} 个档位精灵到资源表「{tableName}」，覆盖 {locales.Count} 种语言。\n跳过未配置源图的档位 {skipped} 个。",
            "确定");
    }

    static PhotoStudioGameConfig FindConfig()
    {
        string[] guids = AssetDatabase.FindAssets($"t:{nameof(PhotoStudioGameConfig)}");
        if(guids.Length == 0)
            return null;
        if(guids.Length > 1)
            Debug.LogWarning($"[PhotoStudioLocalizationTool] 找到多个 PhotoStudioGameConfig，使用第一个：{AssetDatabase.GUIDToAssetPath(guids[0])}");
        return AssetDatabase.LoadAssetAtPath<PhotoStudioGameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // 逐级创建目录（AssetDatabase 要求父目录已存在）
    static void EnsureFolder(string assetPath)
    {
        if(AssetDatabase.IsValidFolder(assetPath))
            return;
        string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string leaf = Path.GetFileName(assetPath);
        if(!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
