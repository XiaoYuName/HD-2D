using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

// [LocKeySelector] 的绘制逻辑：文本框 + "选择" 按钮，按钮弹出 LocKeySelectorWindow 搜索 Key。
public sealed class LocKeySelectorDrawer : OdinAttributeDrawer<LocKeySelectorAttribute, string>
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        string tableName = GetTableName();

        EditorGUILayout.BeginHorizontal();

        ValueEntry.SmartValue = EditorGUILayout.TextField(label, ValueEntry.SmartValue);

        GUI.enabled = !string.IsNullOrEmpty(tableName);

        if (GUILayout.Button("选择", GUILayout.Width(55)))
        {
            LocKeySelectorWindow.Open(tableName, ValueEntry.SmartValue, key =>
            {
                ValueEntry.SmartValue = key;
                ValueEntry.ApplyChanges();
            });
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // 同级 Table 字段名固定为 "Table"（LocKeyRef 自身的字段），反射取值即可，不需要额外配置
    string GetTableName()
    {
        var parentValue = Property.ParentValueProperty?.ValueEntry?.WeakSmartValue;
        if (parentValue == null)
            return null;

        var field = parentValue.GetType().GetField("Table", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field?.GetValue(parentValue) as string;
    }
}

public sealed class LocKeySelectorWindow : EditorWindow
{
    const float RowHeight = 24f;

    string tableName;
    string searchText;
    string currentKey;
    bool searchFocused;

    Vector2 scroll;
    Action<string> onSelected;

    readonly List<KeyItem> allItems = new();
    List<KeyItem> filteredItems = new();

    public static void Open(string tableName, string currentKey, Action<string> onSelected)
    {
        var window = CreateInstance<LocKeySelectorWindow>();

        window.titleContent = new GUIContent("选择本地化 Key");
        window.tableName = tableName;
        window.currentKey = currentKey;
        window.onSelected = onSelected;
        window.searchText = string.Empty;

        window.minSize = new Vector2(760, 520);
        window.LoadData();
        window.ShowUtility();
    }

    void OnGUI()
    {
        DrawToolbar();
        DrawInfo();
        DrawList();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("搜索", GUILayout.Width(35));

        GUI.SetNextControlName("LocKeySearchField");
        string newSearch = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField);

        if (newSearch != searchText)
        {
            searchText = newSearch;
            RefreshFilter();
        }

        if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            searchText = string.Empty;
            RefreshFilter();
            GUI.FocusControl("LocKeySearchField");
        }

        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            LoadData();
        }

        EditorGUILayout.EndHorizontal();

        // 只在窗口打开后聚焦一次——每帧 Repaint 都抢焦点会打断输入法的中文组合状态，导致打不出中文
        if (!searchFocused && Event.current.type == EventType.Repaint)
        {
            EditorGUI.FocusTextInControl("LocKeySearchField");
            searchFocused = true;
        }
    }

    void DrawInfo()
    {
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("当前表", tableName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("数量", $"{filteredItems.Count} / {allItems.Count}");

        if (!string.IsNullOrEmpty(currentKey))
            EditorGUILayout.LabelField("当前Key", currentKey);

        EditorGUILayout.Space(4);
    }

    void DrawList()
    {
        if (filteredItems.Count <= 0)
        {
            EditorGUILayout.HelpBox("没有找到匹配的 Key。可以搜索 Key 或文本内容（含中文）。", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (var item in filteredItems)
            DrawRow(item);

        EditorGUILayout.EndScrollView();
    }

    void DrawRow(KeyItem item)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, RowHeight);

        bool isCurrent = item.Key == currentKey;

        if (isCurrent)
            EditorGUI.DrawRect(rect, new Color(0.25f, 0.45f, 0.85f, 0.35f));
        else if (rect.Contains(Event.current.mousePosition))
            EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.08f));

        Rect keyRect = new Rect(rect.x + 6, rect.y + 3, 220, rect.height);
        Rect textRect = new Rect(rect.x + 235, rect.y + 3, rect.width - 300, rect.height);
        Rect buttonRect = new Rect(rect.xMax - 58, rect.y + 2, 52, rect.height - 4);

        EditorGUI.LabelField(keyRect, item.Key, EditorStyles.boldLabel);
        EditorGUI.LabelField(textRect, item.Preview);

        if (GUI.Button(buttonRect, "选择"))
            Select(item.Key);

        if (Event.current.type == EventType.MouseDown &&
            Event.current.clickCount == 2 &&
            rect.Contains(Event.current.mousePosition))
        {
            Select(item.Key);
            Event.current.Use();
        }
    }

    void Select(string key)
    {
        onSelected?.Invoke(key);
        Close();
    }

    void LoadData()
    {
        allItems.Clear();

        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);

        if (collection == null || collection.SharedData == null)
        {
            RefreshFilter();
            return;
        }

        var tables = collection.StringTables.ToList();
        var previewTable = GetPreferredTable(tables);

        foreach (var sharedEntry in collection.SharedData.Entries)
        {
            if (sharedEntry == null || string.IsNullOrEmpty(sharedEntry.Key))
                continue;

            string preview = FormatPreview(previewTable?.GetEntry(sharedEntry.Key)?.LocalizedValue ?? string.Empty);

            // 搜索匹配所有语言表的文本，而不只是预览用的那一张，避免搜到的中文恰好落在非预览表时漏搜
            string allTexts = string.Join(" ", tables
                .Select(t => t.GetEntry(sharedEntry.Key)?.LocalizedValue)
                .Where(v => !string.IsNullOrEmpty(v)));

            allItems.Add(new KeyItem
            {
                Key = sharedEntry.Key,
                Preview = preview,
                SearchBlob = (sharedEntry.Key + " " + allTexts).ToLower()
            });
        }

        allItems.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

        RefreshFilter();
    }

    void RefreshFilter()
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            filteredItems = allItems;
            Repaint();
            return;
        }

        string lower = searchText.ToLower();

        filteredItems = allItems
            .Where(x => x.SearchBlob.Contains(lower))
            .ToList();

        Repaint();
    }

    // 多语言表顺序不固定，直接取第一张表可能拿到非中文表；优先取简体中文，其次任意中文，最后兜底第一张
    static StringTable GetPreferredTable(List<StringTable> tables)
    {
        if (tables == null || tables.Count == 0)
            return null;

        return tables.FirstOrDefault(t => t.LocaleIdentifier.Code.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
            ?? tables.FirstOrDefault(t => t.LocaleIdentifier.Code.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            ?? tables.FirstOrDefault();
    }

    static string FormatPreview(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        value = value.Replace("\r", "").Replace("\n", " ").Trim();

        if (value.Length > 80)
            value = value.Substring(0, 80) + "...";

        return value;
    }

    sealed class KeyItem
    {
        public string Key;
        public string Preview;
        public string SearchBlob;
    }
}
