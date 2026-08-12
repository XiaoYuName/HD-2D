using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine.Localization;

#if UNITY_EDITOR
using UnityEditor;
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

    public void Set(string table, string key)
    {
        Table = table;
        Value = key;
    }

    // 取当前语言的文本。这两个方法是整个项目里唯一把「表 + Key」交给多语言插件的地方：
    // 配表和业务代码只存/传 LocKeyRef，换插件（或换项目）时改这里就够。
    public string Get() => Get(null);

    public string Get(LocVars vars)
    {
        if(!IsValid())
            return string.Empty;

        // 每次现造一份：占位符是这一次调用的，不能留在共享的配置对象上
        LocalizedString localized = new LocalizedString(Table, Value);
        if(vars != null)
        {
            foreach(KeyValuePair<string, object> pair in vars.Values)
                localized.SetVar(pair.Key, pair.Value, false);
        }
        return localized.GetLocalizedString();
    }

#if UNITY_EDITOR
    bool HasTable() => !string.IsNullOrEmpty(Table);

    bool HasValue() => !string.IsNullOrEmpty(Table) && !string.IsNullOrEmpty(Value);

    void OnTableChanged() => Value = null;

    IEnumerable<string> GetTables() => LocEditorBridge.Tables;

    string GetPreviewText() => LocEditorBridge.GetPreviewText(Table, Value);
#endif
}
