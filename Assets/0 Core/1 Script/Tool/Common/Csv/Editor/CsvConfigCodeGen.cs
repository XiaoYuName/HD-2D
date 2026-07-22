using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 CSV 表头生成配置类代码：Project 里选中 CSV → 右键「CSV 生成配置类」。
/// 表头约定：字段名、类型、可选格式行（sep=/kvsep=）、中文标签。
/// 同时兼容旧三行表头及 Luban 的 Type#sep=+ / (list#sep=+),T 写法。
/// 生成 [CsvSyncedConfig] 标记的 XxxConfig 与 XxxItemData 于同一文件；
/// 之后创建 SO 资产并把 CSV 拖到 csvTable 字段即可，导入与自动同步全由 CsvConfigAutoSync 完成。
/// 表头变更后可对同一文件重新生成覆盖（注意会连 CreateAssetMenu 等手工改动一起覆盖）。
/// </summary>
public static class CsvConfigCodeGen
{
    const string MenuPath = "Assets/CSV/生成配置类";

    [MenuItem(MenuPath, true)]
    static bool Validate()
        => Selection.activeObject
           && AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    [MenuItem(MenuPath)]
    static void Generate()
    {
        string csvPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        string[] lines = CsvTool.ReadAllLinesShared(Path.GetFullPath(csvPath));
        if(lines == null)
            return;
        if(lines.Length < 3)
        {
            Debug.LogError($"[CsvConfigCodeGen] 表头不足 3 行（字段名/类型/中文标签）：{csvPath}");
            return;
        }

        string[] names = SplitRow(lines[0]);
        string[] types = SplitRow(lines[1]);
        bool hasFormatRow = CsvTool.IsFormatRow(SplitRow(lines[2]));
        if(hasFormatRow && lines.Length < 4)
        {
            Debug.LogError($"[CsvConfigCodeGen] 检测到格式行，但缺少第4行中文标签：{csvPath}");
            return;
        }
        string[] labels = SplitRow(lines[hasFormatRow ? 3 : 2]);

        // GameEnterPanelConfig.csv → 根名 GameEnterPanel → GameEnterPanelConfig + GameEnterPanelItemData
        string root = Path.GetFileNameWithoutExtension(csvPath);
        if(root.EndsWith("Config", StringComparison.OrdinalIgnoreCase))
            root = root.Substring(0, root.Length - "Config".Length);

        string savePath = EditorUtility.SaveFilePanelInProject(
            "生成配置类", root + "Config.cs", "cs", "选择生成代码的保存位置", "Assets");
        if(string.IsNullOrEmpty(savePath))
            return;

        File.WriteAllText(savePath, BuildCode(csvPath, root, names, types, labels), new UTF8Encoding(true));
        AssetDatabase.ImportAsset(savePath);
        Debug.Log($"[CsvConfigCodeGen] 已生成 {savePath}（{root}Config / {root}ItemData）。" +
                  "编译后请创建 SO 资产并把 CSV 拖到 csvTable 字段。");
    }

    static string BuildCode(string csvPath, string root, string[] names, string[] types, string[] labels)
    {
        int idIndex = Array.FindIndex(names, n => string.Equals(n, "Id", StringComparison.OrdinalIgnoreCase));
        string keyType = idIndex >= 0 ? MapType(Cell(types, idIndex)) : "string";
        var sb = new StringBuilder();
        sb.AppendLine($"// 本文件由 CsvConfigCodeGen 根据 {csvPath} 表头自动生成，表头变更后可重新生成覆盖（手工改动会被一并覆盖）。");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Sirenix.OdinInspector;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using XFramework;");
        sb.AppendLine("using Object = UnityEngine.Object;");
        sb.AppendLine();
        sb.AppendLine($"[CreateAssetMenu(fileName = \"{root}Config\", menuName = \"Configs/{root}Config\")]");
        sb.AppendLine("[CsvSyncedConfig]");
        sb.AppendLine($"public class {root}Config : SerializedScriptableObject");
        sb.AppendLine("{");
        sb.AppendLine($"    [SerializeField] Dictionary<{keyType}, {root}ItemData> dataDict;   // Id → 行数据，由 CSV 自动导入");
        sb.AppendLine("    [SerializeField] Object csvTable;                                // 拖入对应 CSV；变更自动同步，齿轮菜单可手动导入");
        sb.AppendLine();
        sb.AppendLine($"    public Dictionary<{keyType}, {root}ItemData> DataDict => dataDict;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("[Serializable]");
        sb.AppendLine($"public class {root}ItemData");
        sb.AppendLine("{");

        // 字段区：列名转 camelCase，类型按第2行映射，中文标签作行尾注释
        for(int i = 0; i < names.Length; i++)
        {
            if(string.IsNullOrEmpty(names[i]) || names[i].StartsWith("##", StringComparison.Ordinal))
                continue;
            string label = i < labels.Length ? labels[i] : string.Empty;
            sb.AppendLine($"    [SerializeField] {MapType(Cell(types, i))} {LowerFirst(names[i])};" +
                          (string.IsNullOrEmpty(label) ? "" : $"   // {label}"));
        }
        sb.AppendLine();

        // 属性区：与列名同名的只读属性
        for(int i = 0; i < names.Length; i++)
        {
            if(string.IsNullOrEmpty(names[i]) || names[i].StartsWith("##", StringComparison.Ordinal))
                continue;
            sb.AppendLine($"    public {MapType(Cell(types, i))} {UpperFirst(names[i])} => {LowerFirst(names[i])};");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    static string[] SplitRow(string line) => CsvTool.SplitLine(line);

    static string Cell(string[] row, int i) => i < row.Length ? row[i] : string.Empty;

    static string MapType(string t) => CsvTypeDeclaration.Parse(t).TypeExpression;

    static string LowerFirst(string s) => char.ToLowerInvariant(s[0]) + s.Substring(1);
    static string UpperFirst(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);
}
