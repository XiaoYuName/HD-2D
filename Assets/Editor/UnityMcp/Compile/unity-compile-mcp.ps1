<#
.SYNOPSIS
    独立的 MCP stdio 服务：让 AI 能主动触发 Unity 编译并读回编译结果。

.DESCRIPTION
    和 Prefab Bridge 完全独立：不占用端口、不依赖桥服务。
    同目录的轻量 Editor 状态报告器只向 Library 写 CompilationPipeline 生命周期，
    使外部进程在编译失败、程序集不更新时也能准确且及时地结束等待。

    为什么不走 Unity 内的桥：编辑器在后台时 update 循环其实还在跑（Prefab Bridge 照常响应），
    但 AssetDatabase.Refresh() + CompilationPipeline.RequestScriptCompilation() 是空转 ——
    实测在后台调完连 .meta 都不生成，编译被推迟到窗口重新获得焦点。
    也就是说"让 Unity 编译"这件事本质上必须从进程外解决：先把焦点给它。

    做法分两步：
      1. 把 Unity 主窗口切到前台 —— 关键难点。后台进程调 SetForegroundWindow / SwitchToThisWindow
         会被 Windows 前台锁静默拒绝（实测两者都失败，这正是本服务曾经"看起来没用"的原因：
         焦点抢不到 → 按键不敢发 → 只能等人工点窗口）。现在走 UnityFg::Force：
         前台锁超时置 0 + AttachThreadInput 借前台线程输入队列，失败再补一次 Ctrl 轻点。
      2. 确认前台窗口真的是 Unity 之后，发一次 Ctrl+R（Assets > Refresh），强制刷新 + 重新编译。
         实测第 1 步成功后 Unity 一般就自动 refresh + 编译了，第 2 步是防"只刷新不编译"的保险。

    完成判据优先使用 Unity 的 CompilationPipeline 状态 generation；仅首次引入状态报告器时，
    才回退到本次新增的 Tundra 完成日志和 Library\ScriptAssemblies 稳定窗口。

    注意：本文件必须保存为「带 BOM 的 UTF-8」，Windows PowerShell 5.1 读无 BOM 文件时
    按系统 ANSI 代码页解码，会把中文注释后的换行一起吃掉导致解析失败。
#>
param(
    [string]$ProjectPath = "",
    [string]$EditorLogPath = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# MCP 的 stdio 必须是 UTF-8；被 Node 以管道拉起时没有控制台，Console 编码会退化成 OEM 代码页。
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }
try { [Console]::InputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $cursor = Get-Item -LiteralPath $PSScriptRoot
    while ($null -ne $cursor -and
        (-not (Test-Path -LiteralPath (Join-Path $cursor.FullName "Assets")) -or
         -not (Test-Path -LiteralPath (Join-Path $cursor.FullName "ProjectSettings")))) {
        $cursor = $cursor.Parent
    }
    if ($null -eq $cursor) {
        throw "Cannot locate the Unity project root above $PSScriptRoot. Pass -ProjectPath explicitly."
    }
    $ProjectPath = $cursor.FullName
}
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')

if ([string]::IsNullOrWhiteSpace($EditorLogPath)) {
    $EditorLogPath = Join-Path $env:LOCALAPPDATA "Unity\Editor\Editor.log"
}

$ScriptAssemblyDir = Join-Path $ProjectPath "Library\ScriptAssemblies"
$CompileStateDir = Join-Path $ProjectPath "Library\UnityCompileMcp"
$CompileStatePath = Join-Path $CompileStateDir "compile-state.json"
$TundraLogPath = Join-Path $ProjectPath "Library\Bee\tundra.log.json"

# 类名一变就能在自热重载后重新 Add-Type（AppDomain 里已加载的类型改不掉）；存在则跳过。
if (-not ('UnityFg' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Threading;
public static class UnityFg {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetActiveWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, IntPtr pvParam, uint flags);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

    const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
    const uint SPIF_SENDCHANGE = 0x0002;
    const int SW_RESTORE = 9;
    const byte VK_CONTROL = 0x11;
    const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>
    /// 把窗口真正切到前台。后台进程直接调 SetForegroundWindow / SwitchToThisWindow 会被 Windows
    /// 前台锁静默拒绝（实测两者都返回不了前台），必须先满足"本线程持有前台输入状态"这个条件：
    ///   1. 前台锁超时置 0；
    ///   2. AttachThreadInput 挂到当前前台窗口所属线程，借它的输入队列；
    ///   3. 仍失败时轻点一下 Ctrl（任何按键都行，Ctrl 单击对所有程序都无副作用，
    ///      Alt 会在部分程序里激活菜单栏），给本线程盖上"刚有用户输入"的戳再试。
    /// </summary>
    public static bool Force(IntPtr hWnd, bool allowKeyNudge) {
        if (hWnd == IntPtr.Zero) return false;
        if (GetForegroundWindow() == hWnd) return true;
        for (int pass = 0; pass < 2; pass++) {
            try { SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, SPIF_SENDCHANGE); } catch { }
            uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
            uint self = GetCurrentThreadId();
            bool attached = fgThread != 0 && fgThread != self && AttachThreadInput(fgThread, self, true);
            try {
                if (pass == 1 && allowKeyNudge) {
                    keybd_event(VK_CONTROL, 0, 0, IntPtr.Zero);
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                }
                if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);
                SetActiveWindow(hWnd);
            } finally {
                if (attached) AttachThreadInput(fgThread, self, false);
            }
            for (int i = 0; i < 20; i++) {
                if (GetForegroundWindow() == hWnd) return true;
                Thread.Sleep(50);
            }
            if (!allowKeyNudge) break;
        }
        return GetForegroundWindow() == hWnd;
    }
}
'@
}

if (-not ('UnityCompileFilesV2' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public sealed class UnitySourceSnapshot {
    public string stamp;
    public bool pending;
    public string[] pendingAssemblies;
}

public static class UnityCompileFilesV2 {
    sealed class Boundary {
        public string directory;
        public string assembly;
    }

    static readonly Regex NameRegex = new Regex("\"name\"\\s*:\\s*\"([^\"]+)\"");
    static readonly Regex ReferenceRegex = new Regex("\"reference\"\\s*:\\s*\"([^\"]+)\"");
    static readonly Regex GuidRegex = new Regex("^guid:\\s*(\\S+)", RegexOptions.Multiline);

    public static UnitySourceSnapshot GetSnapshot(string assetsPath, string assemblyDirectory) {
        if (!Directory.Exists(assetsPath))
            return new UnitySourceSnapshot { stamp = "0|0|0", pendingAssemblies = new string[0] };

        List<Boundary> boundaries = GetBoundaries(assetsPath);
        var assemblyTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string assetsPrefix = Path.GetFullPath(assetsPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        long newestTicks = DateTime.MinValue.Ticks;
        long totalLength = 0;
        int count = 0;

        foreach (string path in Directory.EnumerateFiles(assetsPath, "*.cs", SearchOption.AllDirectories)) {
            var file = new FileInfo(path);
            long ticks = file.LastWriteTimeUtc.Ticks;
            if (ticks > newestTicks) newestTicks = ticks;
            totalLength += file.Length;
            count++;

            string assemblyName = GetAssemblyName(file.FullName, assetsPrefix, boundaries);
            DateTime assemblyTime;
            if (!assemblyTimes.TryGetValue(assemblyName, out assemblyTime)) {
                string assemblyPath = Path.Combine(assemblyDirectory, assemblyName + ".dll");
                assemblyTime = File.Exists(assemblyPath) ? File.GetLastWriteTimeUtc(assemblyPath) : DateTime.MinValue;
                assemblyTimes[assemblyName] = assemblyTime;
            }
            if (file.LastWriteTimeUtc > assemblyTime) pending.Add(assemblyName);
        }

        return new UnitySourceSnapshot {
            stamp = newestTicks + "|" + count + "|" + totalLength,
            pending = pending.Count > 0,
            pendingAssemblies = pending.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    static List<Boundary> GetBoundaries(string assetsPath) {
        var boundaries = new List<Boundary>();
        var guidToAssembly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(assetsPath, "*.asmdef", SearchOption.AllDirectories)) {
            string assembly = ReadValue(path, NameRegex);
            if (string.IsNullOrEmpty(assembly)) continue;
            boundaries.Add(new Boundary { directory = Path.GetDirectoryName(path), assembly = assembly });
            string metaPath = path + ".meta";
            if (!File.Exists(metaPath)) continue;
            Match guid = GuidRegex.Match(File.ReadAllText(metaPath));
            if (guid.Success) guidToAssembly[guid.Groups[1].Value] = assembly;
        }
        foreach (string path in Directory.EnumerateFiles(assetsPath, "*.asmref", SearchOption.AllDirectories)) {
            string reference = ReadValue(path, ReferenceRegex);
            string assembly = reference;
            if (reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                guidToAssembly.TryGetValue(reference.Substring(5), out assembly);
            if (string.IsNullOrEmpty(assembly)) assembly = Path.GetFileNameWithoutExtension(path);
            boundaries.Add(new Boundary { directory = Path.GetDirectoryName(path), assembly = assembly });
        }
        boundaries.Sort((left, right) => right.directory.Length.CompareTo(left.directory.Length));
        return boundaries;
    }

    static string ReadValue(string path, Regex regex) {
        Match match = regex.Match(File.ReadAllText(path));
        return match.Success ? Regex.Unescape(match.Groups[1].Value) : string.Empty;
    }

    static string GetAssemblyName(string fullPath, string assetsPrefix, List<Boundary> boundaries) {
        foreach (Boundary boundary in boundaries) {
            string prefix = boundary.directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return boundary.assembly;
        }

        string relative = fullPath.Substring(assetsPrefix.Length);
        string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        bool editor = segments.Any(segment => string.Equals(segment, "Editor", StringComparison.OrdinalIgnoreCase));
        bool firstPass = segments.Length > 0 && (
            string.Equals(segments[0], "Plugins", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segments[0], "Standard Assets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segments[0], "Pro Standard Assets", StringComparison.OrdinalIgnoreCase));
        if (firstPass) return editor ? "Assembly-CSharp-Editor-firstpass" : "Assembly-CSharp-firstpass";
        return editor ? "Assembly-CSharp-Editor" : "Assembly-CSharp";
    }
}
'@
}

function Write-McpMessage {
    param([Parameter(Mandatory = $true)]$Message)
    [Console]::Out.WriteLine(($Message | ConvertTo-Json -Depth 30 -Compress))
    [Console]::Out.Flush()
}

function New-JsonRpcResponse { param($Id, $Result) return [ordered]@{ jsonrpc = "2.0"; id = $Id; result = $Result } }
function New-JsonRpcError { param($Id, [int]$Code, [string]$Message) return [ordered]@{ jsonrpc = "2.0"; id = $Id; error = [ordered]@{ code = $Code; message = $Message } } }

function New-ToolResult {
    param($Payload, [bool]$IsError = $false)
    return [ordered]@{
        content = @(@{ type = "text"; text = ($Payload | ConvertTo-Json -Depth 20 -Compress) })
        isError = $IsError
    }
}

<# 找出打开本项目的 Unity 编辑器主进程（排除 -batchMode 的资源导入 worker）。 #>
function Get-UnityEditorProcess {
    $normalized = $ProjectPath.Replace('\', '/')
    foreach ($p in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") {
        $cl = $p.CommandLine
        if ([string]::IsNullOrEmpty($cl) -or $cl -match '(?i)-batchMode|AssetImportWorker') { continue }
        if ($cl.Replace('\', '/') -notmatch [regex]::Escape($normalized)) { continue }
        $proc = Get-Process -Id $p.ProcessId -ErrorAction SilentlyContinue
        if ($null -ne $proc -and $proc.MainWindowHandle -ne [IntPtr]::Zero) { return $proc }
    }
    return $null
}

function Set-WindowForeground {
    param([IntPtr]$Handle, [bool]$AllowKeyNudge = $true)
    return [UnityFg]::Force($Handle, $AllowKeyNudge)
}

<#
发 Ctrl+R = Assets > Refresh，强制刷新并重新编译。
必须先确认前台窗口就是 Unity：焦点没抢到就把按键发给别人（浏览器的 Ctrl+R 是刷新页面）。
#>
function Send-RefreshHotkey {
    param([IntPtr]$Handle)

    if (-not (Set-WindowForeground $Handle)) { return $false }

    $VK_CONTROL = 0x11
    $VK_R = 0x52
    $KEYUP = 0x0002
    [UnityFg]::keybd_event($VK_CONTROL, 0, 0, [IntPtr]::Zero)
    [UnityFg]::keybd_event($VK_R, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [UnityFg]::keybd_event($VK_R, 0, $KEYUP, [IntPtr]::Zero)
    [UnityFg]::keybd_event($VK_CONTROL, 0, $KEYUP, [IntPtr]::Zero)
    return $true
}

function Get-LogLength {
    if (-not (Test-Path -LiteralPath $EditorLogPath)) { return 0 }
    return (Get-Item -LiteralPath $EditorLogPath).Length
}

<# 只读取 $FromOffset 之后新追加的日志，避免把历史编译结果误当成本次结果。 #>
function Read-LogTail {
    param([long]$FromOffset)
    if (-not (Test-Path -LiteralPath $EditorLogPath)) { return "" }
    $stream = [System.IO.File]::Open($EditorLogPath, 'Open', 'Read', 'ReadWrite')
    try {
        if ($FromOffset -gt $stream.Length) { $FromOffset = 0 }  # 日志被轮转过
        [void]$stream.Seek($FromOffset, 'Begin')
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)
        return $reader.ReadToEnd()
    }
    finally { $stream.Dispose() }
}

function Read-CompileStateFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete)
        try {
            $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)
            try {
                $json = $reader.ReadToEnd()
            } finally {
                $reader.Dispose()
            }
        } finally {
            $stream.Dispose()
        }
        if ([string]::IsNullOrWhiteSpace($json)) { return $null }
        return $json | ConvertFrom-Json
    } catch {
        # File.Replace 的极短窗口内可能读不到完整文件，下一个状态事件会立即重试。
        return $null
    }
}

<# 只接受目标主 Editor PID；AssetImportWorker 写入的旧版共享状态会被直接丢弃。 #>
function Get-CompileState {
    param([int]$ExpectedUnityPid)
    if ($ExpectedUnityPid -le 0) { return $null }

    $pidStatePath = Join-Path $CompileStateDir "compile-state-$ExpectedUnityPid.json"
    $state = Read-CompileStateFile $pidStatePath
    if ($null -eq $state) { $state = Read-CompileStateFile $CompileStatePath }
    if ($null -eq $state -or [int]$state.unityPid -ne $ExpectedUnityPid) { return $null }
    return $state
}

function Get-CompileMessages {
    param([string]$Text, [int]$MaxResults = 50)
    $seen = New-Object System.Collections.Generic.HashSet[string]
    $result = New-Object System.Collections.ArrayList
    foreach ($line in ($Text -split "`r?`n")) {
        if ($line -notmatch '\)\s*:\s*(error|warning)\s+\w+\d+\s*:') { continue }
        $trimmed = $line.Trim()
        if (-not $seen.Add($trimmed)) { continue }
        [void]$result.Add([ordered]@{
            type = if ($trimmed -match '\)\s*:\s*error') { "error" } else { "warning" }
            message = $trimmed
        })
        if ($result.Count -ge $MaxResults) { break }
    }
    return ,$result
}

function Get-TundraLogStamp {
    if (-not (Test-Path -LiteralPath $TundraLogPath)) { return "none" }
    $file = Get-Item -LiteralPath $TundraLogPath
    return "$($file.LastWriteTimeUtc.Ticks)|$($file.Length)"
}

<# Unity 6 首次编入状态报告器前，从 Bee 的逐节点 JSON 中读取真实 C# 退出码和 stdout。 #>
function Get-TundraCompileResult {
    param([string]$BeforeStamp, [int]$MaxResults = 50)
    $stamp = Get-TundraLogStamp
    if ($stamp -eq "none" -or $stamp -eq $BeforeStamp) { return $null }

    try {
        $stream = [System.IO.File]::Open(
            $TundraLogPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete)
        try {
            # 大项目的 tundra.log.json 可能很大；只读共享尾部，不在等待循环里全量解析。
            [long]$tailBytes = [Math]::Min($stream.Length, 16MB)
            [void]$stream.Seek(-$tailBytes, [System.IO.SeekOrigin]::End)
            $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)
            try {
                $text = $reader.ReadToEnd()
            } finally {
                $reader.Dispose()
            }
        } finally {
            $stream.Dispose()
        }
    } catch {
        return $null
    }

    $hasCscResult = $false
    $failed = $false
    $failedAnnotation = ""
    $compilerOutput = New-Object System.Text.StringBuilder
    foreach ($line in ($text -split "`r?`n")) {
        if ($line -notmatch '"msg":"noderesult"' -or $line -notmatch '"annotation":"Csc ') { continue }
        try {
            $record = $line | ConvertFrom-Json
            $hasCscResult = $true
            if ([int]$record.exitcode -ne 0) {
                $failed = $true
                if ([string]::IsNullOrEmpty($failedAnnotation)) { $failedAnnotation = [string]$record.annotation }
            }
            if (-not [string]::IsNullOrWhiteSpace([string]$record.stdout)) {
                [void]$compilerOutput.AppendLine([string]$record.stdout)
            }
        } catch { }
    }
    if (-not $hasCscResult) { return $null }

    $messages = Get-CompileMessages $compilerOutput.ToString() $MaxResults
    if ($failed -and @($messages | Where-Object { $_.type -eq 'error' }).Count -eq 0) {
        [void]$messages.Insert(0, [ordered]@{
            type = "error"
            message = "$failedAnnotation 编译进程返回了非零退出码。"
        })
    }
    return [pscustomobject]@{
        stamp = $stamp
        hasCscResult = $true
        failed = $failed
        errorCount = @($messages | Where-Object { $_.type -eq 'error' }).Count
        warningCount = @($messages | Where-Object { $_.type -eq 'warning' }).Count
        messages = $messages
    }
}

function Test-TundraLogSettled {
    if (-not (Test-Path -LiteralPath $TundraLogPath)) { return $false }
    return ([datetime]::UtcNow - (Get-Item -LiteralPath $TundraLogPath).LastWriteTimeUtc).TotalSeconds -ge 2
}

<# 兼容首次安装状态报告器时的回退证据：程序集写入时间 + 数量指纹。 #>
function Get-AssemblyStamp {
    if (-not (Test-Path -LiteralPath $ScriptAssemblyDir)) { return "none" }
    $dlls = Get-ChildItem -LiteralPath $ScriptAssemblyDir -Filter *.dll -File -ErrorAction SilentlyContinue
    if (-not $dlls) { return "empty" }
    $newest = ($dlls | Measure-Object -Property LastWriteTimeUtc -Maximum).Maximum
    return "{0:o}|{1}" -f $newest, $dlls.Count
}

# 一次原生目录遍历得到 asmdef/asmref 精确 pending 与并发请求的源码 generation 指纹。
function Get-ScriptSnapshot {
    return [UnityCompileFilesV2]::GetSnapshot(
        (Join-Path $ProjectPath "Assets"),
        $ScriptAssemblyDir)
}

function Get-CompileSnapshot {
    param([bool]$IncludeLogOffset = $false, [int]$UnityPid = 0)
    $script = Get-ScriptSnapshot
    $pending = [bool]$script.pending
    if ($pending -and $UnityPid -le 0) {
        $unity = Get-UnityEditorProcess
        if ($null -ne $unity) { $UnityPid = $unity.Id }
    }
    $state = if ($UnityPid -gt 0) { Get-CompileState $UnityPid } else { $null }
    if ($null -ne $state -and [bool]$state.hasResult -and [bool]$state.succeeded -and
        -not [string]::IsNullOrEmpty([string]$state.sourceStamp) -and
        [string]$state.sourceStamp -eq [string]$script.stamp) {
        $pending = $false
    }
    return [pscustomobject]@{
        sourceStamp = $script.stamp
        assemblyStamp = Get-AssemblyStamp
        pending = $pending
        pendingAssemblies = if ($pending) { $script.pendingAssemblies } else { @() }
        unityPid = $UnityPid
        state = $state
        tundraStamp = Get-TundraLogStamp
        logOffset = if ($IncludeLogOffset) { Get-LogLength } else { 0 }
    }
}

function Test-StateCompletedSince {
    param($Before, $After)
    if ($null -eq $After -or [bool]$After.isCompiling -or -not [bool]$After.hasResult) { return $false }
    if ($null -eq $Before) { return [long]$After.generation -gt 0 }
    if ([long]$After.generation -gt [long]$Before.generation) { return $true }
    return [bool]$Before.isCompiling -and [long]$After.generation -eq [long]$Before.generation
}

function Get-StateMessages {
    param($State, [int]$MaxResults)
    $result = New-Object System.Collections.ArrayList
    if ($null -eq $State -or $null -eq $State.messages) { return ,$result }
    foreach ($entry in @($State.messages)) {
        $text = [string]$entry.message
        if (-not [string]::IsNullOrWhiteSpace([string]$entry.file) -and
            $text -notmatch ('^' + [regex]::Escape([string]$entry.file) + '\(')) {
            $text = "$($entry.file)($($entry.line),$($entry.column)): $text"
        }
        [void]$result.Add([ordered]@{ type = [string]$entry.type; message = $text })
        if ($result.Count -ge $MaxResults) { break }
    }
    return ,$result
}

function New-NoChangesResult {
    param([bool]$WaitedForLock = $false)
    $payload = [ordered]@{
        message = "没有需要编译的改动（所有脚本都不比现有程序集新）。"
        compiled = $false
        pendingChanges = $false
        focused = $false
        hotkeySent = $false
        errorCount = 0
        warningCount = 0
        skipped = $true
    }
    if ($WaitedForLock) { $payload.waitedForCompileLock = $true }
    return New-ToolResult $payload $false
}

function New-MergedCompileResult {
    param($RequestSnapshot, $CurrentSnapshot, [int]$MaxResults)

    $stateCompleted = Test-StateCompletedSince $RequestSnapshot.state $CurrentSnapshot.state
    $tundraResult = if ($stateCompleted) {
        $null
    } else {
        Get-TundraCompileResult $RequestSnapshot.tundraStamp $MaxResults
    }
    if ($stateCompleted) {
        $messages = Get-StateMessages $CurrentSnapshot.state $MaxResults
        $errorCount = [int]$CurrentSnapshot.state.errorCount
        $warningCount = [int]$CurrentSnapshot.state.warningCount
    } elseif ($null -ne $tundraResult) {
        $messages = $tundraResult.messages
        $errorCount = [int]$tundraResult.errorCount
        $warningCount = [int]$tundraResult.warningCount
    } else {
        $messages = Get-CompileMessages (Read-LogTail $RequestSnapshot.logOffset) $MaxResults
        $errorCount = @($messages | Where-Object { $_.type -eq 'error' }).Count
        $warningCount = @($messages | Where-Object { $_.type -eq 'warning' }).Count
    }

    $payload = [ordered]@{
        message = if ($errorCount -gt 0) {
                "相同源码 generation 的编译已由并发请求完成，有 $errorCount 个错误。"
            } else {
                "相同源码 generation 的编译已由并发请求完成，没有错误。"
            }
        compiled = $true
        pendingChanges = [bool]$CurrentSnapshot.pending
        focused = $false
        hotkeySent = $false
        errorCount = $errorCount
        warningCount = $warningCount
        mergedWithConcurrentCompile = $true
        completionSource = if ($stateCompleted) {
            "unity-event"
        } elseif ($null -ne $tundraResult) {
            "tundra-log"
        } else {
            "assembly-stamp"
        }
    }
    if ($stateCompleted) { $payload.compilationGeneration = [long]$CurrentSnapshot.state.generation }
    if ($messages.Count -gt 0) { $payload.messages = $messages }
    return New-ToolResult $payload ($errorCount -gt 0)
}

function Test-RequestCompletedWhileWaiting {
    param($RequestSnapshot, $CurrentSnapshot)
    if ($RequestSnapshot.sourceStamp -ne $CurrentSnapshot.sourceStamp) { return $false }
    if (Test-StateCompletedSince $RequestSnapshot.state $CurrentSnapshot.state) { return $true }
    if ($RequestSnapshot.tundraStamp -ne $CurrentSnapshot.tundraStamp -and
        (Test-TundraLogSettled) -and
        $null -ne (Get-TundraCompileResult $RequestSnapshot.tundraStamp 1)) {
        return $true
    }
    return -not [bool]$CurrentSnapshot.pending -and
        $RequestSnapshot.assemblyStamp -ne $CurrentSnapshot.assemblyStamp
}

function Test-CompileFinishedInLog {
    param([string]$Text)
    return $Text -match '(?im)^\*{3}\s+Tundra build (success|failed)\b' -or
        $Text -match '(?im)^Scripts have compiler errors\b'
}

function Invoke-ForceCompileCore {
    param(
        [int]$TimeoutSeconds = 120,
        [bool]$RestoreFocus = $true,
        [int]$MaxResults = 50,
        [bool]$SendHotkey = $true,
        $RequestSnapshot = $null)

    $snapshotBefore = Get-CompileSnapshot $true
    if ($null -ne $RequestSnapshot -and (Test-RequestCompletedWhileWaiting $RequestSnapshot $snapshotBefore)) {
        return New-MergedCompileResult $RequestSnapshot $snapshotBefore $MaxResults
    }
    if (-not $snapshotBefore.pending) { return New-NoChangesResult }

    $unity = Get-UnityEditorProcess
    if ($null -eq $unity) {
        return New-ToolResult ([ordered]@{ error = "找不到打开 $ProjectPath 的 Unity 编辑器窗口，请先启动 Unity。" }) $true
    }

    $previous = [UnityFg]::GetForegroundWindow()
    $alreadyCompiling = $null -ne $snapshotBefore.state -and
        [bool]$snapshotBefore.state.isCompiling -and
        [int]$snapshotBefore.state.unityPid -eq $unity.Id
    $focused = $false
    $hotkeySent = $false
    $completionState = $null
    $stateCompletionSeenAt = [datetime]::MaxValue
    $completionTundra = $null
    $assemblyChanged = $false
    $completionSource = ""
    $lastAssemblyStamp = $snapshotBefore.assemblyStamp
    $assemblySettledAt = [datetime]::MaxValue
    $lastTundraStamp = $snapshotBefore.tundraStamp
    $tundraSettledAt = [datetime]::MaxValue
    $tundraParsedStamp = ""
    $lastLogLength = $snapshotBefore.logOffset
    $logSettledAt = [datetime]::MaxValue
    [long]$logParsedLength = -1
    $fallbackReadyAt = [datetime]::MaxValue
    $fallbackSource = ""
    $reporterReady = $null -ne $snapshotBefore.state -and
        [int]$snapshotBefore.state.reporterVersion -ge 1
    [void][System.IO.Directory]::CreateDirectory($CompileStateDir)
    $watcher = New-Object System.IO.FileSystemWatcher($CompileStateDir, "compile-state*.json")
    $watcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor
        [System.IO.NotifyFilters]::LastWrite -bor
        [System.IO.NotifyFilters]::Size

    try {
        if (-not $alreadyCompiling) {
            $focused = Set-WindowForeground $unity.MainWindowHandle
            if ($SendHotkey -and $focused) {
                # 切前台通常会自动 refresh；Ctrl+R 防止 Unity 只获得焦点却没有刷新。
                $hotkeySent = Send-RefreshHotkey $unity.MainWindowHandle
            }
        }

        $deadline = if (-not $alreadyCompiling -and -not $focused) {
            Get-Date
        } else {
            (Get-Date).AddSeconds([Math]::Max(5, $TimeoutSeconds))
        }
        while ((Get-Date) -lt $deadline) {
            $currentState = Get-CompileState $unity.Id
            if (Test-StateCompletedSince $snapshotBefore.state $currentState) {
                $completionState = $currentState
                if ($stateCompletionSeenAt -eq [datetime]::MaxValue) { $stateCompletionSeenAt = Get-Date }
                $currentScript = Get-ScriptSnapshot
                if (-not [bool]$currentState.succeeded -or
                    ([string]$currentState.sourceStamp -eq [string]$currentScript.stamp) -or
                    -not [bool]$currentScript.pending -or
                    (Get-Date) -ge $stateCompletionSeenAt.AddSeconds(2)) {
                    $completionSource = "unity-event"
                    break
                }

                # 旧版报告器没有 sourceStamp；给成功编译的 DLL/域重载最多 2 秒完成落盘。
                [void]$watcher.WaitForChanged([System.IO.WatcherChangeTypes]::All, 100)
                continue
            }

            $currentAssemblyStamp = Get-AssemblyStamp
            if ($currentAssemblyStamp -ne $lastAssemblyStamp) {
                $assemblyChanged = $true
                $lastAssemblyStamp = $currentAssemblyStamp
                $assemblySettledAt = (Get-Date).AddSeconds(2)
            }

            $currentTundraStamp = Get-TundraLogStamp
            if ($currentTundraStamp -ne $lastTundraStamp) {
                $lastTundraStamp = $currentTundraStamp
                $tundraSettledAt = (Get-Date).AddSeconds(2)
            }
            if ($currentTundraStamp -ne $snapshotBefore.tundraStamp -and
                $currentTundraStamp -ne $tundraParsedStamp -and
                (Get-Date) -ge $tundraSettledAt) {
                $tundraParsedStamp = $currentTundraStamp
                $completionTundra = Get-TundraCompileResult $snapshotBefore.tundraStamp $MaxResults
                if ($null -ne $completionTundra) {
                    $completionSource = "tundra-log"
                    break
                }
            }

            # Editor.log 仅用于没有报告器/Bee 的旧 Unity 首轮，并且等长度稳定后只读一次。
            $currentLogLength = Get-LogLength
            if ($currentLogLength -ne $lastLogLength) {
                $lastLogLength = $currentLogLength
                $logSettledAt = (Get-Date).AddSeconds(2)
            }
            if (-not $reporterReady -and
                $currentTundraStamp -eq $snapshotBefore.tundraStamp -and
                $currentLogLength -ne $logParsedLength -and
                (Get-Date) -ge $logSettledAt) {
                $logParsedLength = $currentLogLength
                if (Test-CompileFinishedInLog (Read-LogTail $snapshotBefore.logOffset)) {
                    $completionSource = "editor-log"
                    break
                }
            }
            if ($assemblyChanged -and
                $currentTundraStamp -eq $snapshotBefore.tundraStamp -and
                (Get-Date) -ge $assemblySettledAt) {
                # 首次安装报告器的那一轮没有 Unity 事件可读，程序集稳定后兼容返回。
                if (-not $reporterReady) {
                    $completionSource = "assembly-settled"
                    break
                }
                if ([string]::IsNullOrEmpty($fallbackSource)) {
                    $fallbackSource = "assembly-settled"
                    $fallbackReadyAt = (Get-Date).AddSeconds(2)
                }
            }
            if (-not [string]::IsNullOrEmpty($fallbackSource) -and (Get-Date) -ge $fallbackReadyAt) {
                $completionSource = $fallbackSource
                break
            }

            $remainingMs = [int][Math]::Max(1, ($deadline - (Get-Date)).TotalMilliseconds)
            [void]$watcher.WaitForChanged(
                [System.IO.WatcherChangeTypes]::All,
                [Math]::Min(500, $remainingMs))
        }
    } finally {
        $watcher.Dispose()
        # 必须 [void]：函数里未消费的返回值会被并进 Invoke-ForceCompileCore 的输出。
        if ($RestoreFocus -and -not $alreadyCompiling -and $previous -ne [IntPtr]::Zero) {
            [void](Set-WindowForeground $previous)
        }
    }

    $tail = Read-LogTail $snapshotBefore.logOffset
    if ($null -ne $completionState) {
        $messages = Get-StateMessages $completionState $MaxResults
        $errorCount = [int]$completionState.errorCount
        $warningCount = [int]$completionState.warningCount
    } elseif ($null -ne $completionTundra) {
        $messages = $completionTundra.messages
        $errorCount = [int]$completionTundra.errorCount
        $warningCount = [int]$completionTundra.warningCount
    } else {
        $messages = Get-CompileMessages $tail $MaxResults
        $errorCount = @($messages | Where-Object { $_.type -eq 'error' }).Count
        $warningCount = @($messages | Where-Object { $_.type -eq 'warning' }).Count
    }
    $pendingAfter = (Get-CompileSnapshot).pending
    $compiled = -not [string]::IsNullOrEmpty($completionSource)

    $payload = [ordered]@{
        message = if ($compiled) {
                if ($errorCount -gt 0) { "编译完成，有 $errorCount 个错误。" } else { "编译完成，没有错误。" }
            } elseif ($alreadyCompiling) {
                "检测到 Unity 已在编译，但 $TimeoutSeconds 秒内没有收到该 generation 的结束事件。"
            } elseif (-not $focused) {
                "没能把 Unity 窗口切到前台（前台锁绕过失败，可能是 Unity 以管理员身份运行而本进程不是，" +
                "或屏幕被锁/远程会话断开）。请手动点一下 Unity 窗口再重试。"
            } elseif (-not $pendingAfter) {
                "没有需要编译的改动（所有脚本都不比现有程序集新）。"
            } else {
                "已切前台并发送 Ctrl+R，但 $TimeoutSeconds 秒内没有收到编译结束事件：Unity 可能仍在导入资源，" +
                "也可能处于 Play 模式或 Reload Assemblies 被锁（此时它只刷新不编译）。可以再调一次；" +
                "若一直如此，重启 Unity。"
            }
        compiled = $compiled
        pendingChanges = $pendingAfter
        focused = $focused
        hotkeySent = $hotkeySent
        errorCount = $errorCount
        warningCount = $warningCount
        unityPid = $unity.Id
    }
    if ($compiled) {
        $payload.completionSource = $completionSource
        $payload.assemblyChanged = $assemblyChanged -or
            $snapshotBefore.assemblyStamp -ne (Get-AssemblyStamp)
    }
    if ($null -ne $completionState) {
        $payload.compilationGeneration = [long]$completionState.generation
    }
    if (-not $compiled) { $payload.pendingBeforeCall = $true }
    if ($messages.Count -gt 0) { $payload.messages = $messages }
    return New-ToolResult $payload ($errorCount -gt 0)
}

function Get-CompileMutexName {
    $normalizedPath = $ProjectPath.ToLowerInvariant()
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalizedPath)
        $hash = [Convert]::ToHexString($sha256.ComputeHash($bytes))
    } finally {
        $sha256.Dispose()
    }
    return "Local\CodexUnityCompile_$hash"
}

function Invoke-ForceCompile {
    param([int]$TimeoutSeconds = 120, [bool]$RestoreFocus = $true, [int]$MaxResults = 50, [bool]$SendHotkey = $true)

    # P0 快路径：没有磁盘改动时不抢 Mutex、不找窗口、更不会抢焦点。
    $requestSnapshot = Get-CompileSnapshot
    if (-not $requestSnapshot.pending) { return New-NoChangesResult }
    $requestSnapshot.logOffset = Get-LogLength

    $mutex = New-Object System.Threading.Mutex($false, (Get-CompileMutexName))
    $lockTaken = $false
    try {
        # 比执行超时多留 5 秒，避免并发调用在前一请求刚释放锁前先行超时。
        $waitMilliseconds = [Math]::Max(5000, [Math]::Min(905000, $TimeoutSeconds * 1000 + 5000))
        try {
            $lockTaken = $mutex.WaitOne($waitMilliseconds)
        } catch [System.Threading.AbandonedMutexException] {
            # 前一个服务进程异常退出，锁已由系统回收；当前调用可以安全接管。
            $lockTaken = $true
        }
        if (-not $lockTaken) {
            return New-ToolResult ([ordered]@{
                error = "同一 Unity 项目已有编译请求正在执行，请稍后重试。"
                compileLockWaitedMilliseconds = $waitMilliseconds
            }) $true
        }

        # 等锁期间前一个请求可能已经处理了同一源码 generation；此处重新读取并合并结果。
        $currentSnapshot = Get-CompileSnapshot
        if (Test-RequestCompletedWhileWaiting $requestSnapshot $currentSnapshot) {
            return New-MergedCompileResult $requestSnapshot $currentSnapshot $MaxResults
        }
        if (-not $currentSnapshot.pending) { return New-NoChangesResult $true }

        return Invoke-ForceCompileCore `
            -TimeoutSeconds $TimeoutSeconds `
            -RestoreFocus $RestoreFocus `
            -MaxResults $MaxResults `
            -SendHotkey $SendHotkey `
            -RequestSnapshot $requestSnapshot
    } finally {
        if ($lockTaken) { [void]$mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

function Invoke-ReadCompileLog {
    param([int]$TailKilobytes = 256, [int]$MaxResults = 50)
    if (-not (Test-Path -LiteralPath $EditorLogPath)) {
        return New-ToolResult ([ordered]@{ error = "找不到 Editor.log: $EditorLogPath" }) $true
    }
    $length = Get-LogLength
    $offset = [Math]::Max(0, $length - ([Math]::Max(1, $TailKilobytes) * 1024))
    $tail = Read-LogTail $offset
    $messages = Get-CompileMessages $tail $MaxResults
    $errorCount = @($messages | Where-Object { $_.type -eq 'error' }).Count
    $payload = [ordered]@{
        message = "读取了 Editor.log 末尾 $TailKilobytes KB，命中 $($messages.Count) 条编译诊断。"
        logPath = $EditorLogPath
        errorCount = $errorCount
        warningCount = @($messages | Where-Object { $_.type -eq 'warning' }).Count
        # 脚本比程序集新 = 这份日志不代表磁盘上代码的最新状态。
        pendingChanges = (Get-CompileSnapshot).pending
    }
    if ($messages.Count -gt 0) { $payload.messages = $messages }
    return New-ToolResult $payload $false
}

function Get-ToolDefinitions {
    return @(
        [ordered]@{
            name = "force_unity_compile"
            description = "Compile pending Unity scripts and report the exact finished generation. With no pending .cs changes it returns immediately without taking focus. IMPORTANT: when changes are pending this tool briefly focuses Unity and may send Ctrl+R; tell the user immediately before calling it. It restores the previous foreground window by default. Requests are serialized per project and wait on a CompilationPipeline state file. Concurrent calls for the same source generation reuse the first result. focused=false means the foreground grab failed unless compilation was already running. Works independently of the Prefab Bridge."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    timeoutSeconds = @{ type = "integer"; minimum = 5; maximum = 900; default = 120; description = "How long to wait for the compile to finish." }
                    restoreFocus = @{ type = "boolean"; default = $true; description = "Give focus back to the window that had it before. Set false to leave Unity in front." }
                    sendRefreshHotkey = @{ type = "boolean"; default = $true; description = "Send Ctrl+R to Unity after focusing it. Only disable if keystrokes must not be injected; focus alone often does not trigger a compile." }
                    maxResults = @{ type = "integer"; minimum = 1; maximum = 200; default = 50; description = "Maximum diagnostics to return." }
                }
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "read_unity_compile_log"
            description = "Read compile errors and warnings from the tail of Unity's Editor.log without touching the Editor or stealing focus. Use it to inspect the last compile, or when Unity is unreachable. pendingChanges=true means some script is newer than the compiled assemblies, so these diagnostics are stale."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    tailKilobytes = @{ type = "integer"; minimum = 1; maximum = 8192; default = 256; description = "How much of the log tail to scan." }
                    maxResults = @{ type = "integer"; minimum = 1; maximum = 200; default = 50 }
                }
                additionalProperties = $false
            }
        }
    )
}

function Invoke-McpTool {
    param([string]$Name, $Arguments)
    switch ($Name) {
        "force_unity_compile" {
            return Invoke-ForceCompile `
                -TimeoutSeconds $(if ($Arguments.timeoutSeconds) { [int]$Arguments.timeoutSeconds } else { 120 }) `
                -RestoreFocus $(if ($null -ne $Arguments.restoreFocus) { [bool]$Arguments.restoreFocus } else { $true }) `
                -SendHotkey $(if ($null -ne $Arguments.sendRefreshHotkey) { [bool]$Arguments.sendRefreshHotkey } else { $true }) `
                -MaxResults $(if ($Arguments.maxResults) { [int]$Arguments.maxResults } else { 50 })
        }
        "read_unity_compile_log" {
            return Invoke-ReadCompileLog `
                -TailKilobytes $(if ($Arguments.tailKilobytes) { [int]$Arguments.tailKilobytes } else { 256 }) `
                -MaxResults $(if ($Arguments.maxResults) { [int]$Arguments.maxResults } else { 50 })
        }
        default { throw "Unknown tool: $Name" }
    }
}

<#
自热重载：本脚本是 MCP 客户端拉起的长驻进程，改完文件后进程里仍是旧函数定义，
而协议里没有"重启服务端"这种请求。每次请求前比对自身 mtime，变了就重新 dot-source
（必须在脚本作用域做，在函数里 dot-source 只写进函数作用域）；
被 dot-source 的那份靠 $SelfReloading 提前 return，避免递归进主循环。
工具表是客户端在 initialize 时缓存的，改了 schema 仍需客户端重连一次；改行为不用。
#>
$SelfLoadedAtUtc = [System.IO.File]::GetLastWriteTimeUtc($PSCommandPath)
if ($SelfReloading) { return }

while ($null -ne ($line = [Console]::In.ReadLine())) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $line = $line.TrimStart([char]0xFEFF)

    $currentWriteUtc = [System.IO.File]::GetLastWriteTimeUtc($PSCommandPath)
    if ($currentWriteUtc -ne $SelfLoadedAtUtc) {
        # dot-source 会重跑 param 块把实参覆盖成默认值，重载后还回去。
        $savedProjectPath = $ProjectPath
        $savedLogPath = $EditorLogPath
        $SelfReloading = $true
        try { . $PSCommandPath }
        catch { [Console]::Error.WriteLine("[unity-compile-mcp] self-reload failed, keeping previous version: $($_.Exception.Message)") }
        $SelfReloading = $false
        $ProjectPath = $savedProjectPath
        $EditorLogPath = $savedLogPath
        $ScriptAssemblyDir = Join-Path $ProjectPath "Library\ScriptAssemblies"
        $CompileStateDir = Join-Path $ProjectPath "Library\UnityCompileMcp"
        $CompileStatePath = Join-Path $CompileStateDir "compile-state.json"
        $TundraLogPath = Join-Path $ProjectPath "Library\Bee\tundra.log.json"
        $SelfLoadedAtUtc = $currentWriteUtc
    }

    try { $request = $line | ConvertFrom-Json }
    catch {
        Write-McpMessage (New-JsonRpcError -Id $null -Code -32700 -Message "Parse error")
        continue
    }

    $id = $request.id
    try {
        switch ($request.method) {
            "initialize" {
                $version = if ($request.params.protocolVersion) { $request.params.protocolVersion } else { "2024-11-05" }
                Write-McpMessage (New-JsonRpcResponse -Id $id -Result ([ordered]@{
                    protocolVersion = $version
                    capabilities = [ordered]@{ tools = [ordered]@{ listChanged = $false } }
                    serverInfo = [ordered]@{ name = "unity-compile-mcp"; version = "1.2.0" }
                }))
            }
            "notifications/initialized" { }
            "ping" { Write-McpMessage (New-JsonRpcResponse -Id $id -Result ([ordered]@{})) }
            "tools/list" { Write-McpMessage (New-JsonRpcResponse -Id $id -Result ([ordered]@{ tools = @(Get-ToolDefinitions) })) }
            "tools/call" { Write-McpMessage (New-JsonRpcResponse -Id $id -Result (Invoke-McpTool -Name $request.params.name -Arguments $request.params.arguments)) }
            default {
                if ($null -ne $id) { Write-McpMessage (New-JsonRpcError -Id $id -Code -32601 -Message "Method not found: $($request.method)") }
            }
        }
    }
    catch {
        if ($null -ne $id) { Write-McpMessage (New-JsonRpcError -Id $id -Code -32603 -Message $_.Exception.Message) }
    }
}
