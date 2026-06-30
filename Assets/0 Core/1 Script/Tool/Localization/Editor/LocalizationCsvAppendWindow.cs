#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

/// <summary>
/// 「向多语言 CSV 追加一行条目」的便捷面板（UIToolkit）。拖入任意本地化 CSV → 自动按表头列出各语言输入框 →
/// 填 Key 与译文 → 一键追加并保存到该 CSV。免去每次手动打开 CSV 对齐列、补引号。
/// 追加/解析逻辑见 <see cref="LocalizationCsvEditor"/>；可选「同时导入字符串表」按 CSV 上级目录名匹配同名表（如 Data/Factory → Factory）。
/// 菜单：Tools/Localization/CSV 追加多语言条目。
/// </summary>
public class LocalizationCsvAppendWindow : EditorWindow
{
    readonly List<(string code, string header, TextField field)> langFields = new();
    ObjectField csvField;
    TextField keyField;
    VisualElement langContainer;
    Toggle alsoImport;
    Label preview;
    Label status;
    Button appendBtn;

    [MenuItem("Tools/Localization/CSV 追加多语言条目")]
    static void Open() => GetWindow<LocalizationCsvAppendWindow>("CSV 追加多语言条目").minSize = new Vector2(480, 460);

    void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.style.paddingTop = root.style.paddingBottom = root.style.paddingLeft = root.style.paddingRight = 10f;

        root.Add(new HelpBox(
            "向多语言 CSV 追加一行：选 CSV → 自动列出各语言输入框 → 填 Key 与译文 → 「追加并保存」。\n" +
            "Key 留空或与表内已有 Key 重复时不会写入。译文留空的语言列会按空值写入。",
            HelpBoxMessageType.Info));

        csvField = new ObjectField("目标 CSV") { objectType = typeof(TextAsset), allowSceneObjects = false };
        csvField.RegisterValueChangedCallback(_ => RebuildLangFields());
        root.Add(csvField);

        keyField = new TextField("Key") { tooltip = "本地化键名，建议沿用模块前缀（如 FactoryMoldManage）。" };
        keyField.RegisterValueChangedCallback(_ => Refresh());
        root.Add(keyField);

        root.Add(new Label("各语言译文") { style = { marginTop = 8f, unityFontStyleAndWeight = FontStyle.Bold } });
        langContainer = new VisualElement();
        root.Add(langContainer);

        alsoImport = new Toggle("追加后导入同名字符串表（按 CSV 上级目录名匹配，如 Data/Factory → Factory）")
        { value = true, style = { marginTop = 8f } };
        root.Add(alsoImport);

        appendBtn = new Button(Append) { text = "追加并保存", style = { height = 30f, marginTop = 8f } };
        root.Add(appendBtn);

        preview = new Label { style = { marginTop = 8f, whiteSpace = WhiteSpace.Normal, unityFontStyleAndWeight = FontStyle.Italic } };
        root.Add(preview);
        status = new Label { style = { marginTop = 4f, whiteSpace = WhiteSpace.Normal } };
        root.Add(status);

        RebuildLangFields();
    }

    string AssetPath => csvField.value is TextAsset ta ? AssetDatabase.GetAssetPath(ta) : null;

    LocalizationCsvEditor.ParseResult Parse()
        => csvField.value is TextAsset ta ? LocalizationCsvEditor.Parse(ta.text) : default;

    // CSV 变化时：按表头重建语言输入框
    void RebuildLangFields()
    {
        langContainer.Clear();
        langFields.Clear();

        if(!(csvField.value is TextAsset))
        { status.text = "请选择一个本地化 CSV。"; preview.text = ""; appendBtn?.SetEnabled(false); Refresh(); return; }

        LocalizationCsvEditor.ParseResult p = Parse();
        if(!p.ok)
        { status.text = "✗ " + p.message; preview.text = ""; appendBtn?.SetEnabled(false); return; }

        foreach(LocalizationCsvEditor.Column col in p.columns)
        {
            if(string.IsNullOrEmpty(col.code)) continue;   // 跳过 Key/Id 列
            var field = new TextField(col.header);
            field.RegisterValueChangedCallback(_ => Refresh());
            langContainer.Add(field);
            langFields.Add((col.code, col.header, field));
        }
        Refresh();
    }

    Dictionary<string, string> Values()
    {
        var dict = new Dictionary<string, string>();
        foreach((string code, string _, TextField field) in langFields)
            dict[code] = field.value;
        return dict;
    }

    // 实时预览将要追加的行 + Key 查重提示
    void Refresh()
    {
        if(!(csvField.value is TextAsset))
        { appendBtn?.SetEnabled(false); return; }

        LocalizationCsvEditor.ParseResult p = Parse();
        string key = keyField.value?.Trim();
        bool keyOk = !string.IsNullOrEmpty(key) && p.ok && !p.keys.Contains(key);
        appendBtn?.SetEnabled(keyOk);

        if(!p.ok) { status.text = "✗ " + p.message; preview.text = ""; return; }
        if(string.IsNullOrEmpty(key)) { status.text = "请填写 Key。"; preview.text = ""; return; }
        if(p.keys.Contains(key)) { status.text = $"⚠ Key「{key}」已存在于该 CSV，无法重复添加。"; preview.text = ""; return; }

        status.text = $"将追加到「{Path.GetFileName(AssetPath)}」（现有 {p.keys.Count} 个 Key）。";
        preview.text = "预览：" + LocalizationCsvEditor.BuildRow(p.columns, key, Values());
    }

    void Append()
    {
        string path = AssetPath;
        if(!LocalizationCsvEditor.AppendEntry(path, keyField.value, Values(), out string msg))
        { status.text = "✗ " + msg; return; }

        string extra = "";
        if(alsoImport.value)
            extra = TryImport(path);

        status.text = "✓ " + msg + extra;
        preview.text = "";
        keyField.value = "";
        foreach((string _, string __, TextField field) in langFields)
            field.value = "";
        Refresh();
    }

    // 按 CSV 上级目录名匹配同名字符串表集合，把这份 CSV 合并进去（找不到表则跳过，仅写 CSV）
    string TryImport(string assetPath)
    {
        string collectionName = Path.GetFileName(Path.GetDirectoryName(assetPath));
        StringTableCollection col = LocalizationEditorSettings.GetStringTableCollection(collectionName);
        if(col == null)
            return $"（未找到同名字符串表「{collectionName}」，仅写入 CSV；请手动运行导入菜单。）";

        LocalizationCsvMerger.Result r = LocalizationCsvMerger.Merge(File.ReadAllText(Path.GetFullPath(assetPath)), col, overwrite: true);
        return r.ok
            ? $" 已导入「{collectionName}」表（新增 {r.added}，更新 {r.updated}）。"
            : $"（写入 CSV 成功，但导入「{collectionName}」失败：{r.message}）";
    }
}
#endif
