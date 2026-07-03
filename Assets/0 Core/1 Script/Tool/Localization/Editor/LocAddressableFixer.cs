using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

/// <summary>
/// 修复控制台刷屏的
/// 「AddressableEntryNotFoundException: xxx_&lt;locale&gt; could not find an Addressable asset」。
/// 成因：新建 / 改名 / git 合并本地化表后，表资源还在但没有登记进 Addressables，
/// Localization Tables 窗口读取预加载标记(Preload flag)时取不到 entry 就抛异常。
/// 处理：对所有 String / Asset 表集合调用 RefreshAddressables()，
/// 会清掉失效表、重新登记 SharedData 与每张分语言表的 Addressable entry。非破坏性，可重复执行。
/// </summary>
public static class LocAddressableFixer
{
    [MenuItem("Tools/Loc/修复表的 Addressable 登记")]
    public static void Fix()
    {
        int count = 0;
        foreach (var c in LocalizationEditorSettings.GetStringTableCollections())
        {
            c.RefreshAddressables();
            count++;
        }
        foreach (var c in LocalizationEditorSettings.GetAssetTableCollections())
        {
            c.RefreshAddressables();
            count++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[LocalizationAddressableFixer] 已重新登记 {count} 个本地化表集合到 Addressables，可重新打开 Localization Tables 窗口确认。");
    }
}
