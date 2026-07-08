using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
#endif


public static class LocTool
{
    // 同步取本地化串（无变量）；调用时表须已加载完成（运行时格子/面板构建时机通常已就绪）
    public static string Get(string table, string key) => LocalizationSettings.StringDatabase.GetLocalizedString(table, key);

    // 同步取本地化串（带占位符变量，一次性，不随语言切换自动刷新）
    // 例：LocTool.GetFormatted(LocTableSet.Factory, FactoryLocKeySet.UnitPriceFmt, (LocVarSet.FactoryMain.Price, price));
    public static string GetFormatted(string table, string key, params (string name, object value)[] vars)
    {
        LocalizedString ls = new() { TableReference = table, TableEntryReference = key };
        foreach((string name, object value) in vars)
            ls.SetVar(name, value, false);
        return ls.GetLocalizedString();
    }

#if UNITY_EDITOR
    // 给 TMP 文本挂 LocalizeStringEvent 并接入指定表的 key：
    // 缺失时自动补 key 与中文默认值，并把 OnUpdateString 动态绑定到 TMP.SetText。返回该组件。
    public static LocalizeStringEvent AttachText(TextMeshProUGUI text, string table, string key, string zhDefault = null)
    {
        EnsureKey(table, key, zhDefault);
        LocalizeStringEvent lse = text.gameObject.AddComponent<LocalizeStringEvent>();
        lse.StringReference.SetReference(table, key);
        UnityEventTools.AddPersistentListener<string>(lse.OnUpdateString, text.SetText);
        return lse;
    }

    // 给 TMP 文本挂 LocalizeStringEvent 并把 OnUpdateString 绑定到 TMP.SetText，但不指定 key。
    // 用于运行时动态切换 key 的场景（如选择器逐项切换），运行时调用 SetReference + RefreshString 即可。
    public static LocalizeStringEvent AttachDynamicText(TextMeshProUGUI text)
    {
        LocalizeStringEvent lse = text.gameObject.AddComponent<LocalizeStringEvent>();
        UnityEventTools.AddPersistentListener<string>(lse.OnUpdateString, text.SetText);
        return lse;
    }

    // 确保字符串表中存在 key，并为 zh-CN 设置默认中文（已有非空值则不覆盖）
    public static void EnsureKey(string table, string key, string zhDefault = null)
    {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(table);
        if(collection == null)
        {
            Debug.LogWarning($"[LocalizeTool] 未找到字符串表 {table}");
            return;
        }

        SharedTableData shared = collection.SharedData;
        if(!shared.Contains(key))
            shared.AddKey(key);

        if(!string.IsNullOrEmpty(zhDefault) && collection.GetTable("zh-CN") is StringTable zhTable)
        {
            StringTableEntry entry = zhTable.GetEntry(key);
            if(entry == null || string.IsNullOrEmpty(entry.Value))
                zhTable.AddEntry(key, zhDefault);
            EditorUtility.SetDirty(zhTable);
        }
        EditorUtility.SetDirty(shared);
    }
#endif
}
