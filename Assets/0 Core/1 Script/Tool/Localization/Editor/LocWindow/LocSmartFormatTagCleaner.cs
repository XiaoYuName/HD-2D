#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

/// <summary>
/// 清理 StringTable 中指向已删除 Shared Entry 的 Smart Format 标记。
/// Unity Localization 只会在序列化时写回标记 ID，删除 Shared Entry 后不会自动移除旧 ID。
/// </summary>
public static class LocSmartFormatTagCleaner
{
    public struct Result
    {
        public int tableCount;
        public int removedIdCount;
    }

    static readonly FieldInfo EntriesLookupField = typeof(SharedTableEntryMetadata)
        .GetField("m_EntriesLookup", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>扫描全部 StringTable，并删除已不在对应 SharedTableData 中的 Smart Format ID。</summary>
    public static Result CleanAll()
    {
        var result = new Result();
        foreach(StringTableCollection collection in LocalizationEditorSettings.GetStringTableCollections())
        {
            foreach(StringTable table in collection.StringTables)
            {
                int invalidIdCount = GetInvalidIdCount(table);
                if(invalidIdCount == 0)
                    continue;
                Undo.RegisterCompleteObjectUndo(table, "清理无效 Smart Format ID");
                int removed = Clean(table);
                result.tableCount++;
                result.removedIdCount += removed;
                EditorUtility.SetDirty(table);
            }
        }
        if(result.removedIdCount > 0)
            AssetDatabase.SaveAssets();
        return result;
    }

    static int Clean(StringTable table)
    {
        int removed = 0;
        foreach(IMetadata metadata in table.MetadataEntries)
        {
            if(metadata is not SmartFormatTag tag)
                continue;
            var entries = (HashSet<long>)EntriesLookupField.GetValue(tag);
            removed += entries.RemoveWhere(id => !table.SharedData.Contains(id));
        }
        return removed;
    }

    static int GetInvalidIdCount(StringTable table)
    {
        int count = 0;
        foreach(IMetadata metadata in table.MetadataEntries)
        {
            if(metadata is not SmartFormatTag tag)
                continue;
            var entries = (HashSet<long>)EntriesLookupField.GetValue(tag);
            foreach(long id in entries)
                if(!table.SharedData.Contains(id))
                    count++;
        }
        return count;
    }
}
#endif
