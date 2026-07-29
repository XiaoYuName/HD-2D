<#
.SYNOPSIS
    独立的 MCP stdio 服务：让 AI 能主动触发 Unity 编译并读回编译结果。

.DESCRIPTION
    和 InspectorBridge 完全独立：不需要任何 Unity 侧脚本，也不占用端口，
    删掉这个文件夹不影响 InspectorBridge，反之亦然。

    为什么不走 Unity 内的桥：编辑器在后台时 update 循环其实还在跑（InspectorBridge 照常响应），
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

    成功判据也不看日志长度，而看 Library\ScriptAssemblies 下的程序集有没有被重写 ——
    那是"编译真的发生了"的唯一硬证据；日志里的 Refreshing native plugins 之类普通刷新也会打。

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
        if ([string]::IsNullOrEmpty($cl) -or $cl -match '-batchMode') { continue }
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

<# 编译真的发生过的硬证据：ScriptAssemblies 下最新的程序集写入时间 + 数量指纹。 #>
function Get-AssemblyStamp {
    if (-not (Test-Path -LiteralPath $ScriptAssemblyDir)) { return "none" }
    $dlls = Get-ChildItem -LiteralPath $ScriptAssemblyDir -Filter *.dll -File -ErrorAction SilentlyContinue
    if (-not $dlls) { return "empty" }
    $newest = ($dlls | Measure-Object -Property LastWriteTimeUtc -Maximum).Maximum
    return "{0:o}|{1}" -f $newest, $dlls.Count
}

<# Assets 下最新的 .cs 修改时间：比程序集新就说明还有改动没被编译进去。 #>
function Get-NewestScriptTimeUtc {
    $assets = Join-Path $ProjectPath "Assets"
    if (-not (Test-Path -LiteralPath $assets)) { return [datetime]::MinValue }
    $newest = Get-ChildItem -LiteralPath $assets -Filter *.cs -File -Recurse -ErrorAction SilentlyContinue |
        Measure-Object -Property LastWriteTimeUtc -Maximum
    if ($null -eq $newest.Maximum) { return [datetime]::MinValue }
    return $newest.Maximum
}

function Get-NewestAssemblyTimeUtc {
    if (-not (Test-Path -LiteralPath $ScriptAssemblyDir)) { return [datetime]::MinValue }
    $newest = Get-ChildItem -LiteralPath $ScriptAssemblyDir -Filter *.dll -File -ErrorAction SilentlyContinue |
        Measure-Object -Property LastWriteTimeUtc -Maximum
    if ($null -eq $newest.Maximum) { return [datetime]::MinValue }
    return $newest.Maximum
}

function Invoke-ForceCompileCore {
    param([int]$TimeoutSeconds = 120, [bool]$RestoreFocus = $true, [int]$MaxResults = 50, [bool]$SendHotkey = $true)

    $unity = Get-UnityEditorProcess
    if ($null -eq $unity) {
        return New-ToolResult ([ordered]@{ error = "找不到打开 $ProjectPath 的 Unity 编辑器窗口，请先启动 Unity。" }) $true
    }

    $offset = Get-LogLength
    $assemblyStampBefore = Get-AssemblyStamp
    $pendingBefore = (Get-NewestScriptTimeUtc) -gt (Get-NewestAssemblyTimeUtc)

    $previous = [UnityFg]::GetForegroundWindow()
    $focused = Set-WindowForeground $unity.MainWindowHandle
    $hotkeySent = $false
    if ($SendHotkey -and $focused) {
        # 切前台通常就够（Unity 重新获得焦点会自动 refresh + 编译）；
        # 但遇到过只刷新不编译的状态，Ctrl+R 是从进程外补上"强制重编译"的手段。
        $hotkeySent = Send-RefreshHotkey $unity.MainWindowHandle
    }

    $deadline = (Get-Date).AddSeconds([Math]::Max(5, $TimeoutSeconds))
    $compiled = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 700
        if ((Get-AssemblyStamp) -ne $assemblyStampBefore) { $compiled = $true; break }
    }
    # 程序集刚写完时日志可能还差最后几行，给它一点收尾时间再读。
    if ($compiled) { Start-Sleep -Milliseconds 1200 }

    $tail = Read-LogTail $offset
    # 必须 [void]：函数里未消费的返回值会被并进 Invoke-ForceCompile 的输出。
    if ($RestoreFocus -and $previous -ne [IntPtr]::Zero) { [void](Set-WindowForeground $previous) }

    $messages = Get-CompileMessages $tail $MaxResults
    $errorCount = @($messages | Where-Object { $_.type -eq 'error' }).Count
    $pendingAfter = (Get-NewestScriptTimeUtc) -gt (Get-NewestAssemblyTimeUtc)

    $payload = [ordered]@{
        message = if ($compiled) {
                if ($errorCount -gt 0) { "编译完成，有 $errorCount 个错误。" } else { "编译完成，没有错误。" }
            } elseif (-not $focused) {
                "没能把 Unity 窗口切到前台（前台锁绕过失败，可能是 Unity 以管理员身份运行而本进程不是，" +
                "或屏幕被锁/远程会话断开）。请手动点一下 Unity 窗口再重试。"
            } elseif (-not $pendingAfter) {
                "没有需要编译的改动（所有脚本都不比现有程序集新）。"
            } else {
                "已切前台并发送 Ctrl+R，但 $TimeoutSeconds 秒内程序集没有被重写：Unity 可能仍在导入资源，" +
                "也可能处于 Play 模式或 Reload Assemblies 被锁（此时它只刷新不编译）。可以再调一次；" +
                "若一直如此，重启 Unity。"
            }
        compiled = $compiled
        pendingChanges = $pendingAfter
        focused = $focused
        hotkeySent = $hotkeySent
        errorCount = $errorCount
        warningCount = @($messages | Where-Object { $_.type -eq 'warning' }).Count
        unityPid = $unity.Id
    }
    if ($pendingBefore -and -not $compiled) { $payload.pendingBeforeCall = $true }
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

    $mutex = New-Object System.Threading.Mutex($false, (Get-CompileMutexName))
    $lockTaken = $false
    try {
        $waitMilliseconds = [Math]::Max(5000, [Math]::Min(900000, $TimeoutSeconds * 1000))
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
        return Invoke-ForceCompileCore `
            -TimeoutSeconds $TimeoutSeconds `
            -RestoreFocus $RestoreFocus `
            -MaxResults $MaxResults `
            -SendHotkey $SendHotkey
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
        pendingChanges = ((Get-NewestScriptTimeUtc) -gt (Get-NewestAssemblyTimeUtc))
    }
    if ($messages.Count -gt 0) { $payload.messages = $messages }
    return New-ToolResult $payload $false
}

function Get-ToolDefinitions {
    return @(
        [ordered]@{
            name = "force_unity_compile"
            description = "Make Unity actually compile pending script changes and report the result. The Editor defers asset refresh and compilation while it is in the background, so requesting compilation from inside Unity does nothing. This tool forces the Unity window to the foreground (bypassing the Windows foreground lock) AND sends Ctrl+R (Assets > Refresh), then waits until Library/ScriptAssemblies is actually rewritten - the only hard evidence a compile happened - and returns the errors/warnings. focused=false means the foreground grab failed and nothing happened; compiled=false with pendingChanges=true means Unity refused to compile (still importing, in Play mode, or assembly reload locked). Works even when the InspectorBridge HTTP server is down."
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
                    serverInfo = [ordered]@{ name = "unity-compile-mcp"; version = "1.1.0" }
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
