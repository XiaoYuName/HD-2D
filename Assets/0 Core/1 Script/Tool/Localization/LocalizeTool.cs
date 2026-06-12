using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
#endif

// 多语言通用工具：编辑期为 UI 文本挂接 LocalizeStringEvent、补全字符串表 key 等
public static class LocalizeTool
{
#if UNITY_EDITOR
    // 给 TMP 文本挂 LocalizeStringEvent 并接入指定表的 key：
    // 缺失时自动补 key 与中文默认值，并把 OnUpdateString 动态绑定到 TMP.SetText。返回该组件。
    public static LocalizeStringEvent BindText(TextMeshProUGUI text, string table, string key, string zhDefault = null)
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
