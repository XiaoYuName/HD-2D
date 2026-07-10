// 已停用：本自动导入器服务于旧 ItemConfig(ScriptableObject) + ItemConfig.csv 流程，
// 物品系统已迁移到 Luban(TbItemData)，ItemConfig / ItemConfigImporter / ItemConfigPaths 均已删除。
// 整体用 #if false 编译屏蔽，保留代码备查；如需恢复 Loc.csv 自动合并，请基于新流程重写。
#if false
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

/// <summary>
/// 检测到 ItemConfig.csv / ItemConfigLoc.csv 在 Assets 内被修改并重新导入时，自动执行对应的一键导入，
/// 免去每次手动点开 ItemConfig 面板按钮 / Loc 合并面板。
/// </summary>
public class ItemConfigAutoImporter : AssetPostprocessor
{
    const string ItemConfigAssetPath = "Assets/AddressableAssets/Remote/Configs/Item/ItemConfig.asset";

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Contains(ItemConfigPaths.ItemConfigCsv))
        {
            var config = AssetDatabase.LoadAssetAtPath<ItemConfig>(ItemConfigAssetPath);
            if (config != null)
                ItemConfigImporter.ImportFromCsv(config);
            else
                Debug.LogError($"[ItemConfig] 未找到 ItemConfig 资产: {ItemConfigAssetPath}，自动导入已跳过。");
        }

        if (imported.Contains(ItemConfigPaths.ItemConfigLocCsv))
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocTableSet.InventoryItem);
            if (collection == null)
            {
                Debug.LogError($"[ItemConfig] 未找到字符串表集合「{LocTableSet.InventoryItem}」，自动导入已跳过。");
                return;
            }

            string csvText = File.ReadAllText(Path.GetFullPath(ItemConfigPaths.ItemConfigLocCsv));
            LocCsvMerger.Result r = LocCsvMerger.Merge(csvText, collection, overwrite: true);
            if (r.ok)
                Debug.Log($"[ItemConfig] 检测到 ItemConfigLoc.csv 变动，已自动合并到「{LocTableSet.InventoryItem}」表：新增 {r.added}，更新 {r.updated}。");
            else
                Debug.LogError($"[ItemConfig] ItemConfigLoc.csv 自动合并失败：{r.message}");
        }
    }
}
#endif
#endif
