#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

/// <summary>
/// 通用「CSV → 字符串表」导入面板（UIToolkit）。一个窗口覆盖两种使用场景，合并/清空逻辑见 <see cref="LocCsvMerger"/>：
///   · 增量合并：加 1 份 CSV、不勾「导入前清空」——把内容并进目标表，其余 Key 不动（旧行为）。
///   · 重建表（去冗余）：加多份 CSV、勾「导入前清空」——先清空目标表再全部导入，使表只保留这些 CSV 里的 Key。
/// 另提供「仅清空目标表」按钮（不导入，带二次确认）。菜单：Tools/Localization/CSV 导入本地化字符串表。
/// </summary>
public class LocCsvMergeWindow : EditorWindow
{
    readonly List<StringTableCollection> collections = new();
    readonly List<ObjectField> csvRows = new();
    DropdownField tableField;
    VisualElement csvList;
    Toggle clearFirst;
    Toggle overwrite;
    Button importBtn;
    Button clearBtn;
    Label status;

    [MenuItem("Tools/Localization/CSV 导入本地化字符串表")]
    static void Open() => GetWindow<LocCsvMergeWindow>("CSV 导入本地化字符串表").minSize = new Vector2(460, 380);

    void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.style.paddingTop = root.style.paddingBottom = root.style.paddingLeft = root.style.paddingRight = 10f;

        root.Add(new HelpBox(
            "把多语言 CSV 导入目标字符串表集合。表头：Key,Id,Chinese (Simplified)(zh-CN),English(en),…（Id 列可留空）。\n" +
            "· 增量合并：加 1 份 CSV、不勾「导入前清空」——并入内容，其余 Key 不动。\n" +
            "· 重建表（去冗余）：加多份 CSV、勾「导入前清空」——先清空再全部导入，表里不在这些 CSV 中的 Key 会被移除。",
            HelpBoxMessageType.Info));

        collections.AddRange(LocalizationEditorSettings.GetStringTableCollections());
        List<string> names = collections.ConvertAll(c => c.TableCollectionName);
        tableField = new DropdownField("目标字符串表", names, names.Count > 0 ? 0 : -1);
        tableField.RegisterValueChangedCallback(_ => Refresh());
        root.Add(tableField);

        root.Add(new Label("CSV 列表") { style = { marginTop = 8f, unityFontStyleAndWeight = FontStyle.Bold } });
        csvList = new VisualElement();
        root.Add(csvList);
        root.Add(new Button(() => { AddRow(null); Refresh(); }) { text = "+ 添加 CSV" });

        clearFirst = new Toggle("导入前清空目标表（去除冗余项）") { value = false, tooltip = "勾选：先删除目标表全部 Key 再导入，使表只保留这些 CSV 里的项。" };
        overwrite = new Toggle("覆盖已有翻译") { value = true, tooltip = "勾选：已存在的 Key 用 CSV 覆盖；不勾：仅填充原本为空的语言值。" };
        clearFirst.RegisterValueChangedCallback(_ => { UpdateImportLabel(); Refresh(); });
        root.Add(clearFirst);
        root.Add(overwrite);

        importBtn = new Button(Import) { style = { height = 30f, marginTop = 8f } };
        clearBtn = new Button(ClearOnly) { text = "仅清空目标表", style = { height = 24f, marginTop = 4f } };
        root.Add(importBtn);
        root.Add(clearBtn);

        status = new Label { style = { marginTop = 8f, whiteSpace = WhiteSpace.Normal } };
        root.Add(status);

        AddRow(null);            // 默认给一行
        UpdateImportLabel();
        Refresh();
    }

    // 一行：ObjectField(占满) + 删除按钮
    void AddRow(TextAsset initial)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2f } };
        var field = new ObjectField { objectType = typeof(TextAsset), allowSceneObjects = false, value = initial, style = { flexGrow = 1f } };
        field.RegisterValueChangedCallback(_ => Refresh());
        var remove = new Button(() => { csvRows.Remove(field); csvList.Remove(row); Refresh(); }) { text = "✕", style = { width = 24f } };
        row.Add(field);
        row.Add(remove);
        csvList.Add(row);
        csvRows.Add(field);
    }

    void UpdateImportLabel() => importBtn.text = clearFirst.value ? "清空并导入" : "导入";

    StringTableCollection Table
    {
        get
        {
            int i = tableField.index;
            return i >= 0 && i < collections.Count ? collections[i] : null;
        }
    }

    List<string> CsvTexts()
    {
        var list = new List<string>();
        foreach(ObjectField f in csvRows)
            if(f.value is TextAsset ta && !string.IsNullOrEmpty(ta.text))
                list.Add(ta.text);
        return list;
    }

    // 选择源/目标后实时分析（不写盘）；勾了清空时额外提示将被移除的 Key 数
    void Refresh()
    {
        List<string> texts = CsvTexts();
        importBtn.SetEnabled(Table != null && texts.Count > 0);
        clearBtn.SetEnabled(Table != null);

        if(collections.Count == 0)
        { status.text = "⚠ 工程里没有任何字符串表集合。"; return; }
        if(Table == null)
        { status.text = "请选择目标字符串表。"; return; }
        if(texts.Count == 0)
        { status.text = "请至少添加一份 CSV。"; return; }

        string head = clearFirst.value
            ? $"⚠ 导入前将清空「{Table.TableCollectionName}」当前的 {Table.SharedData.Entries.Count} 个 Key（去除冗余）。\n"
            : "";
        status.text = head + Describe(LocCsvMerger.AnalyzeMany(texts, Table), merged: false);
    }

    void Import()
    {
        List<string> texts = CsvTexts();
        if(Table == null || texts.Count == 0)
        { status.text = "⚠ 请选择目标表并至少添加一份 CSV。"; return; }

        if(clearFirst.value && !EditorUtility.DisplayDialog(
            "清空并导入",
            $"将先清空「{Table.TableCollectionName}」当前的 {Table.SharedData.Entries.Count} 个 Key，再导入 {texts.Count} 份 CSV。\n此操作不可撤销，确定继续？",
            "清空并导入", "取消"))
            return;

        status.text = Describe(LocCsvMerger.Import(texts, Table, overwrite.value, clearFirst.value), merged: true);
    }

    void ClearOnly()
    {
        if(Table == null)
        { status.text = "⚠ 请先选择目标表。"; return; }
        int n = Table.SharedData.Entries.Count;
        if(!EditorUtility.DisplayDialog(
            "仅清空目标表",
            $"确定清空「{Table.TableCollectionName}」当前的 {n} 个 Key 及全部语言翻译？\n此操作不可撤销。",
            "清空", "取消"))
            return;
        int removed = LocCsvMerger.Clear(Table);
        status.text = $"✓ 已清空「{Table.TableCollectionName}」：移除 {removed} 个 Key。";
        Refresh();
    }

    string Describe(LocCsvMerger.Result r, bool merged)
    {
        if(!r.ok)
            return "✗ " + r.message;

        string text = merged
            ? $"✓ 已导入「{Table.TableCollectionName}」：" + (r.cleared > 0 ? $"清空 {r.cleared}，" : "")
              + $"新增 {r.added}，更新 {r.updated}，写入语言列 {r.matchedLocales.Count}，自动标记 Smart {r.smartMarked}。"
            : $"可导入数据行：{r.keyCount}（命中已有 {r.updated}，将新增 {r.added}）\n匹配语言列：{Join(r.matchedLocales)}";
        if(r.missingLocales.Count > 0)
            text += $"\n⚠ 目标表缺少语言（跳过）：{Join(r.missingLocales)}";
        return text;
    }

    static string Join(List<string> items) => items.Count > 0 ? string.Join(", ", items) : "（无）";
}
#endif
