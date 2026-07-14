#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// GameEnterPanel 多语言表同步工具（用法仿 ItemConfigAutoImporter）：
/// · 菜单一键创建「GameEnterPanel」字符串表集合（若不存在）并导入 GameEnterPanelLoc.csv，首次创建额外把已迁移的 Key 从 CasinoGame 表删除；
/// · 检测到 GameEnterPanelLoc.csv 被重新导入时自动合并（只增量导入，不做删除）。
/// </summary>
public class GameEnterPanelLocSync : AssetPostprocessor
{
    const string CsvPath = "Assets/0 Core/1 Script/Data/Common/GameEnterPanelLoc.csv";
    const string TargetFolder = "Assets/AddressableAssets/Local/LocalizationTable/StringTable/GameEnterPanel";

    // 从 CasinoGame 表「转移」到 GameEnterPanel 表的 Key：只在手动执行创建菜单、且是首次建表时删一次。
    // NotEnoughStamina / NotEnoughGameCoin 因为还被 WitchPoisonPanel / CrashSprintPanel 直接引用，不在此列，会一直保留在 CasinoGame 表里。
    static readonly string[] MigratedKeys =
    {
        "StartGame", "Consume",
        "ClawMachineName", "ClawMachineDesc",
        "CrashSprintName", "CrashSprintDesc",
        "WitchPoisonName", "WitchPoisonDesc",
    };

    [MenuItem("Tools/Loc/GameEnterPanel 一键创建并导入")]
    static void CreateAndImport()
    {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(LocTableSet.GameEnterPanel);
        bool created = false;
        if(collection == null)
        {
            collection = LocalizationEditorSettings.CreateStringTableCollection(LocTableSet.GameEnterPanel, TargetFolder);
            created = true;
            Debug.Log($"[GameEnterPanel] 已创建字符串表集合「{LocTableSet.GameEnterPanel}」于 {TargetFolder}。");
        }

        if(!Import(collection))
            return;

        if(created)
            MigrateFromCasinoGame();
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if(!imported.Contains(CsvPath))
            return;

        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(LocTableSet.GameEnterPanel);
        if(collection == null)
        {
            Debug.LogError($"[GameEnterPanel] 未找到字符串表集合「{LocTableSet.GameEnterPanel}」，请先执行菜单 Tools/Loc/GameEnterPanel 一键创建并导入。");
            return;
        }
        Import(collection);
    }

    static bool Import(StringTableCollection collection)
    {
        string csvText = File.ReadAllText(Path.GetFullPath(CsvPath));
        LocCsvMerger.Result r = LocCsvMerger.Merge(csvText, collection, overwrite: true);
        if(r.ok)
        {
            Debug.Log($"[GameEnterPanel] 已同步 GameEnterPanelLoc.csv 到「{LocTableSet.GameEnterPanel}」表：新增 {r.added}，更新 {r.updated}。");
            return true;
        }
        Debug.LogError($"[GameEnterPanel] 同步失败：{r.message}");
        return false;
    }

    // 把已复制到 GameEnterPanel 表的 Key 从 CasinoGame 表删除，完成真正的「转移」（先弹二次确认，列出将删除的 Key）。
    static void MigrateFromCasinoGame()
    {
        StringTableCollection casino = LocalizationEditorSettings.GetStringTableCollection(LocTableSet.CasinoGame);
        if(casino == null)
        {
            Debug.LogWarning($"[GameEnterPanel] 未找到「{LocTableSet.CasinoGame}」表，跳过旧 Key 清理。");
            return;
        }

        string[] toRemove = MigratedKeys.Where(k => casino.SharedData.Contains(k)).ToArray();
        if(toRemove.Length == 0)
            return;

        if(!EditorUtility.DisplayDialog(
            "从 CasinoGame 表移除已迁移的 Key",
            $"以下 {toRemove.Length} 个 Key 已复制到「{LocTableSet.GameEnterPanel}」表，是否从「{LocTableSet.CasinoGame}」表删除（完成迁移）？\n\n" +
            string.Join("\n", toRemove) +
            "\n\n此操作不可撤销，确定继续？",
            "删除", "先不删"))
            return;

        foreach(string key in toRemove)
        {
            foreach(StringTable t in casino.StringTables)
                t.RemoveEntry(key);
            casino.SharedData.RemoveKey(key);
        }
        EditorUtility.SetDirty(casino.SharedData);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GameEnterPanel] 已从「{LocTableSet.CasinoGame}」表删除 {toRemove.Length} 个已迁移的 Key：{string.Join(", ", toRemove)}");
    }
}
#endif
