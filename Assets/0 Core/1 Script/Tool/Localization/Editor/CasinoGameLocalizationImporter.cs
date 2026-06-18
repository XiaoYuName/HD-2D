#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// 一键把「爆点冲刺」多语言 CSV 并进 CasinoGame 字符串表的便捷入口。
/// 通用合并逻辑见 <see cref="LocalizationCsvMerger"/>；任意表/任意 CSV 请用菜单 Tools/Localization/CSV 导入本地化字符串表。
/// </summary>
public static class CasinoGameLocalizationImporter
{
    const string CsvRelPath = "0 Core/1 Script/Data/CasinoGame/CasinoGameCrashSprintPanel.csv";

    // [MenuItem("Tools/赌场小游戏/导入「爆点冲刺」多语言 → CasinoGame 表")]
    public static void ImportCrashSprint()
    {
        string path = Path.Combine(Application.dataPath, CsvRelPath);
        if(!File.Exists(path))
        {
            Debug.LogError($"[CasinoLoc] 未找到 CSV：{path}");
            return;
        }

        StringTableCollection col = LocalizationEditorSettings.GetStringTableCollection(LocalizeTableSet.CasinoGame);
        LocalizationCsvMerger.Result r = LocalizationCsvMerger.Merge(File.ReadAllText(path, Encoding.UTF8), col, overwrite: true);
        Debug.Log(r.ok
            ? $"[CasinoLoc] 导入完成：CasinoGameCrashSprintPanel.csv → {LocalizeTableSet.CasinoGame}（新增 Key {r.added}，更新 Key {r.updated}，语言列 {r.matchedLocales.Count}，自动标记 Smart {r.smartMarked}）。"
            : $"[CasinoLoc] 导入失败：{r.message}");
    }
}
#endif
