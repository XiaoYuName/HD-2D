#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;

/// <summary>Windows 回收站操作。</summary>
public static class WindowsRecycleBin
{
    const uint FoDelete = 3;
    const ushort FofAllowUndo = 0x0040;
    const ushort FofNoConfirmation = 0x0010;
    const ushort FofSilent = 0x0004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ShellFileOperation
    {
        public System.IntPtr hwnd;
        public uint func;
        public string from;
        public string to;
        public ushort flags;
        public bool aborted;
        public System.IntPtr nameMappings;
        public string progressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHFileOperation(ref ShellFileOperation operation);

    /// <summary>将单个路径移至回收站。</summary>
    public static bool MoveToRecycleBin(string path, out string error)
        => MoveToRecycleBin(new[] { path }, out error);

    /// <summary>将多个路径作为一次操作移至回收站。</summary>
    public static bool MoveToRecycleBin(string[] paths, out string error)
    {
        var operation = new ShellFileOperation
        {
            func = FoDelete,
            from = string.Join("\0", paths) + "\0\0",
            flags = FofAllowUndo | FofNoConfirmation | FofSilent,
        };
        int result = SHFileOperation(ref operation);
        if(result == 0 && !operation.aborted)
        {
            error = null;
            return true;
        }
        error = operation.aborted ? "操作已取消。" : $"系统错误码：{result}";
        return false;
    }
}
#endif
