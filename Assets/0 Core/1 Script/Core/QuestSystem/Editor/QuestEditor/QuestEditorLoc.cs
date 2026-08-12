using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using XFramework;

internal sealed class QuestLocalizationOption
{
    public string Key;
    public string Preview;

    public string SearchText => $"{Key} {Preview}";
}

/// <summary>任务编辑器使用的多语言 Key 读取与展示逻辑。</summary>
internal static class QuestLocalizationEditorUtility
{
    public static IReadOnlyList<QuestLocalizationOption> GetOptions(string keyPrefix)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(LocTableSet.QuestSystem);
        if (collection?.SharedData == null) return Array.Empty<QuestLocalizationOption>();

        StringTable previewTable = LocalizationKeySelectorUtility.GetPreferredTable(
            collection.StringTables.ToList());
        return collection.SharedData.Entries
            .Where(entry => entry != null &&
                            !string.IsNullOrEmpty(entry.Key) &&
                            entry.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
            .Select(entry => new QuestLocalizationOption
            {
                Key = entry.Key,
                Preview = LocalizationKeySelectorUtility.FormatPreview(
                    previewTable?.GetEntry(entry.Key)?.LocalizedValue ?? string.Empty),
            })
            .OrderBy(option => option.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public static string GetKey(LocKeyRef value) => value == null ? string.Empty : value.Value ?? string.Empty;

    public static string GetDisplayPath(LocKeyRef value)
    {
        string key = GetKey(value);
        return string.IsNullOrEmpty(key) ? "未配置" : $"{LocTableSet.QuestSystem}/{key}";
    }

    public static string GetPreview(LocKeyRef value)
    {
        string key = GetKey(value);
        return string.IsNullOrEmpty(key)
            ? string.Empty
            : LocalizationKeySelectorUtility.GetPreviewText(LocTableSet.QuestSystem, key) ?? string.Empty;
    }

    public static LocKeyRef Create(string key)
        => new() { Table = LocTableSet.QuestSystem, Value = key };
}

/// <summary>将单一 Sprite 选择转换为 Addressables Sprite 引用。</summary>
internal static class QuestIconEditorUtility
{
    public static Sprite GetSprite(AssetReferenceSprite reference)
    {
        if (reference == null || !reference.RuntimeKeyIsValid()) return null;
        string path = AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
        if (string.IsNullOrEmpty(path)) return null;
        if (string.IsNullOrEmpty(reference.SubObjectName))
            return reference.editorAsset as Sprite ?? AssetDatabase.LoadAssetAtPath<Sprite>(path);

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == reference.SubObjectName);
    }

    public static AssetReferenceSprite Create(Sprite sprite)
    {
        if (sprite == null) return null;
        AssetReferenceSprite reference = new(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite)));
        if (AssetDatabase.IsSubAsset(sprite)) reference.SetEditorSubObject(sprite);
        return reference;
    }
}

