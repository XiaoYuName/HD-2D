#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class LocEditorBridge
{
    static Func<IEnumerable<string>> getTables;
    static Func<string, string, string> getPreviewText;
    static Action<string, string, string> ensureKey;

    public static IEnumerable<string> Tables => getTables();

    public static string GetPreviewText(string table, string key) => getPreviewText(table, key);

    public static void EnsureKey(string table, string key, string zhDefault) => ensureKey(table, key, zhDefault);

    public static void SetHandlers(
        Func<IEnumerable<string>> getTablesHandler,
        Func<string, string, string> getPreviewTextHandler,
        Action<string, string, string> ensureKeyHandler)
    {
        getTables = getTablesHandler;
        getPreviewText = getPreviewTextHandler;
        ensureKey = ensureKeyHandler;
    }
}
#endif