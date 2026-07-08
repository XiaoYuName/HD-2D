using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
#endif

// 表 + Key 选择器，供本文件夹下的多语言组件使用（LocText / LocTextSwitch / LocTextVar 等）。
// Key 用 LocKeySelectorAttribute 弹独立搜索窗口——Odin 自带的 ValueDropdown 模糊搜索对中文长文本
[Serializable]
[InlineProperty]
[HideLabel]
public class LocKeyRef
{
#if UNITY_EDITOR
    [HorizontalGroup("LocKeyRow", Width = 0.35f)]
    [LabelText("表")]
    [ValueDropdown(nameof(GetTables))]
    [OnValueChanged(nameof(OnTableChanged))]
#endif
    public string Table;

#if UNITY_EDITOR
    [HorizontalGroup("LocKeyRow", Width = 0.65f)]
    [LabelText("Key")]
    [EnableIf(nameof(HasTable))]
    [LocKeySelector]
#endif
    public string Value;

#if UNITY_EDITOR
    // 用 SelectableLabel 而不是 ReadOnly + MultiLineProperty：后者禁用了控件，文本选不中也复制不了
    [OnInspectorGUI]
    [ShowIf(nameof(HasValue))]
    void DrawPreviewText()
    {
        EditorGUILayout.LabelField("文本预览");
        EditorGUILayout.SelectableLabel(GetPreviewText(), EditorStyles.textArea, GUILayout.Height(48));
    }
#endif

    public bool IsValid() => !string.IsNullOrEmpty(Table) && !string.IsNullOrEmpty(Value);

    public override string ToString() => IsValid() ? $"{Table}/{Value}" : "Null";

#if UNITY_EDITOR
    bool HasTable() => !string.IsNullOrEmpty(Table);

    bool HasValue() => !string.IsNullOrEmpty(Table) && !string.IsNullOrEmpty(Value);

    void OnTableChanged() => Value = null;

    IEnumerable<string> GetTables() => LocalizationEditorSettings
        .GetStringTableCollections()
        .Where(c => c != null)
        .Select(c => c.TableCollectionName)
        .Distinct()
        .OrderBy(x => x);

    string GetPreviewText()
    {
        if (string.IsNullOrEmpty(Table) || string.IsNullOrEmpty(Value))
            return string.Empty;

        var collection = LocalizationEditorSettings.GetStringTableCollection(Table);
        if (collection == null)
            return "未找到本地化表";

        // 多语言表顺序不固定，优先取简体中文表做预览
        var tables = collection.StringTables;
        var table = tables.FirstOrDefault(t => t.LocaleIdentifier.Code.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
            ?? tables.FirstOrDefault(t => t.LocaleIdentifier.Code.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            ?? tables.FirstOrDefault();
        if (table == null)
            return "当前表没有语言内容";

        var entry = table.GetEntry(Value);
        return entry == null ? "未找到 Key 对应文本" : entry.LocalizedValue;
    }
#endif
}
