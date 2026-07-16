#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// Common 多语言表同步工具（用法仿 GameEnterPanelLocSync）：
/// · 菜单一键创建「Common」字符串表集合（若不存在）并导入 CommonLoc.csv；
/// · 检测到 CommonLoc.csv 被重新导入时自动合并（只增量导入，不做删除）。
/// 该表存放跨功能通用文案（失败/返回/本局结算/奖励已发放等），供各小游戏结算面板共用。
/// </summary>
public class CommonLocSync : AssetPostprocessor
{
    const string CsvPath = "Assets/0 Core/1 Script/Data/Common/CommonLoc.csv";
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool hit = false;
        foreach(string path in imported)
            if(path == CsvPath)
                hit = true;
        if(!hit)
            return;

        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(LocTableSet.Common);
        Import(collection);
    }

    static bool Import(StringTableCollection collection)
    {
        string csvText = File.ReadAllText(Path.GetFullPath(CsvPath));
        LocCsvMerger.Result r = LocCsvMerger.Merge(csvText, collection, overwrite: true);
        if(r.ok)
        {
            Debug.Log($"[CommonLoc] 已同步 CommonLoc.csv 到「{LocTableSet.Common}」表：新增 {r.added}，更新 {r.updated}。");
            return true;
        }
        Debug.LogError($"[CommonLoc] 同步失败：{r.message}");
        return false;
    }
}
#endif
