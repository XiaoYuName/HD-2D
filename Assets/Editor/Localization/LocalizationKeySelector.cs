using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

[AttributeUsage(AttributeTargets.Field)]
public sealed class LocalizationKeySelectorAttribute : Attribute
{
    public readonly string TableFieldName;

    public LocalizationKeySelectorAttribute(string tableFieldName)
    {
        TableFieldName = tableFieldName;
    }
}

public sealed class LocalizationKeySelectorDrawer : OdinAttributeDrawer<LocalizationKeySelectorAttribute, string>
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        var tableName = GetTableName();

        EditorGUILayout.BeginVertical();

        EditorGUILayout.BeginHorizontal();

        ValueEntry.SmartValue = EditorGUILayout.TextField(label, ValueEntry.SmartValue);

        GUI.enabled = !string.IsNullOrEmpty(tableName);

        if (GUILayout.Button("选择", GUILayout.Width(55)))
        {
            LocalizationKeySelectorWindow.Open(
                tableName,
                ValueEntry.SmartValue,
                key =>
                {
                    ValueEntry.SmartValue = key;
                    ValueEntry.ApplyChanges();
                });
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        DrawPreview(tableName, ValueEntry.SmartValue);

        EditorGUILayout.EndVertical();
    }

    private string GetTableName()
    {
        var parent = Property.ParentValueProperty;
        if (parent == null)
            return null;

        var tableProperty = parent.Children
            .FirstOrDefault(x => x.Name == Attribute.TableFieldName);

        return tableProperty?.ValueEntry?.WeakSmartValue as string;
    }

    private void DrawPreview(string tableName, string key)
    {
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(key))
            return;

        string preview = LocalizationKeySelectorUtility.GetPreviewText(tableName, key);

        if (string.IsNullOrEmpty(preview))
        {
            EditorGUILayout.HelpBox("当前 Key 没有找到对应文本", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(preview, MessageType.None);
    }
}

public sealed class LocalizationKeySelectorWindow : EditorWindow
{
    private const float RowHeight = 24f;

    private string tableName;
    private string searchText;
    private string currentKey;

    private Vector2 scroll;
    private Action<string> onSelected;

    private List<LocalizationKeyItem> allItems = new();
    private List<LocalizationKeyItem> filteredItems = new();

    public static void Open(string tableName, string currentKey, Action<string> onSelected)
    {
        var window = CreateInstance<LocalizationKeySelectorWindow>();

        window.titleContent = new GUIContent("选择本地化Key");
        window.tableName = tableName;
        window.currentKey = currentKey;
        window.onSelected = onSelected;
        window.searchText = string.Empty;

        window.minSize = new Vector2(760, 520);
        window.LoadData();
        window.ShowUtility();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawInfo();
        DrawList();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("搜索", GUILayout.Width(35));

        GUI.SetNextControlName("SearchField");
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
            GUI.FocusControl("SearchField");
        }

        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            LoadData();
        }

        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.FocusTextInControl("SearchField");
        }
    }

    private void DrawInfo()
    {
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("当前表", tableName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("数量", $"{filteredItems.Count} / {allItems.Count}");

        if (!string.IsNullOrEmpty(currentKey))
        {
            EditorGUILayout.LabelField("当前Key", currentKey);
        }

        EditorGUILayout.Space(4);
    }

    private void DrawList()
    {
        if (filteredItems.Count <= 0)
        {
            EditorGUILayout.HelpBox("没有找到匹配的 Key。可以搜索 Key 或文本内容。", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (var item in filteredItems)
        {
            DrawRow(item);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawRow(LocalizationKeyItem item)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, RowHeight);

        bool isCurrent = item.Key == currentKey;

        if (isCurrent)
        {
            EditorGUI.DrawRect(rect, new Color(0.25f, 0.45f, 0.85f, 0.35f));
        }
        else if (rect.Contains(Event.current.mousePosition))
        {
            EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.08f));
        }

        Rect keyRect = new Rect(rect.x + 6, rect.y + 3, 220, rect.height);
        Rect textRect = new Rect(rect.x + 235, rect.y + 3, rect.width - 300, rect.height);
        Rect buttonRect = new Rect(rect.xMax - 58, rect.y + 2, 52, rect.height - 4);

        EditorGUI.LabelField(keyRect, item.Key, EditorStyles.boldLabel);
        EditorGUI.LabelField(textRect, item.Preview);

        if (GUI.Button(buttonRect, "选择"))
        {
            Select(item.Key);
        }

        if (Event.current.type == EventType.MouseDown &&
            Event.current.clickCount == 2 &&
            rect.Contains(Event.current.mousePosition))
        {
            Select(item.Key);
            Event.current.Use();
        }
    }

    private void Select(string key)
    {
        onSelected?.Invoke(key);
        Close();
    }

    private void LoadData()
    {
        allItems.Clear();

        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);

        if (collection == null || collection.SharedData == null)
        {
            RefreshFilter();
            return;
        }

        var table = collection.StringTables.FirstOrDefault();

        foreach (var sharedEntry in collection.SharedData.Entries)
        {
            if (sharedEntry == null || string.IsNullOrEmpty(sharedEntry.Key))
                continue;

            string preview = string.Empty;

            if (table != null)
            {
                var tableEntry = table.GetEntry(sharedEntry.Key);
                preview = tableEntry?.LocalizedValue ?? string.Empty;
            }

            preview = LocalizationKeySelectorUtility.FormatPreview(preview);

            allItems.Add(new LocalizationKeyItem
            {
                Key = sharedEntry.Key,
                Preview = preview
            });
        }

        allItems = allItems
            .OrderBy(x => x.Key)
            .ToList();

        RefreshFilter();
    }

    private void RefreshFilter()
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            filteredItems = allItems;
            Repaint();
            return;
        }

        string lower = searchText.ToLower();

        filteredItems = allItems
            .Where(x =>
                x.Key.ToLower().Contains(lower) ||
                x.Preview.ToLower().Contains(lower))
            .ToList();

        Repaint();
    }

    private sealed class LocalizationKeyItem
    {
        public string Key;
        public string Preview;
    }
}

public static class LocalizationKeySelectorUtility
{
    public static string GetPreviewText(string tableName, string key)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);

        if (collection == null)
            return null;

        var table = collection.StringTables.FirstOrDefault();

        if (table == null)
            return null;

        var entry = table.GetEntry(key);

        return entry == null
            ? null
            : FormatPreview(entry.LocalizedValue);
    }

    public static string FormatPreview(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        value = value
            .Replace("\r", "")
            .Replace("\n", " ")
            .Trim();

        if (value.Length > 80)
            value = value.Substring(0, 80) + "...";

        return value;
    }
}