<#
.SYNOPSIS
    MCP stdio server for token-efficient Unity Prefab inspection and editing.

.DESCRIPTION
    这一层只做协议转发：JSON-RPC ↔ UnityMcp 内的 Prefab HTTP 服务。
    工具表（名字/描述/schema）和响应裁剪都在 Unity 侧（ToolCatalog / BridgeJson），
    这里不再维护第二份契约 —— 以前 schema 手写在这个文件里、响应还按工具挑字段，
    服务端加个字段忘了同步就会被静默丢掉。

    stdout 只能放换行分隔的 JSON-RPC 消息，诊断信息一律走 stderr。
    本文件必须保存为「带 BOM 的 UTF-8」：PowerShell 5.1 读无 BOM 文件时按系统 ANSI 代码页
    解码，GBK 双字节配对会吃掉中文注释后的换行，把下一行代码并进注释。
#>
param(
    [string]$ProjectPath = "",
    [int]$Port = 58732,
    [int]$TimeoutSeconds = 25,
    [switch]$IncludeSourceCodeTools
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# MCP 的 stdio 必须是 UTF-8。Console 编码默认跟随宿主控制台代码页：
# 从 PowerShell 里手动跑时通常已经是 UTF-8，但被 MCP 客户端（Node）以管道拉起时没有控制台，
# 会退化成 OEM 代码页 —— Unity 返回的中文错误信息就会变成乱码甚至一串 "?"（不可逆丢失）。
# 入方向同样要设：setValue 写中文文案时参数要能原样送到 Unity。
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
$ActivePortFile = Join-Path $ProjectPath "Library\PrefabMcpPort.txt"
$ToolCacheFile = Join-Path $ProjectPath "Library\PrefabMcpTools.json"
$SourceToolRelativePath = "Assets\0 Core\1 Script\Tool\Editor\UnityMcp\SourceCodeMcp~\source-code-tools.json"
$SourceToolFile = Join-Path $ProjectPath $SourceToolRelativePath

function Set-SourceToolFilter {
    $script:SourceToolNames = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    if ((-not $IncludeSourceCodeTools) -and (Test-Path -LiteralPath $SourceToolFile)) {
        foreach ($tool in @(Get-Content -LiteralPath $SourceToolFile -Raw | ConvertFrom-Json)) {
            [void]$script:SourceToolNames.Add([string]$tool.name)
        }
    }
}
Set-SourceToolFilter

<#
桥启动时把实际监听端口写进 Library\PrefabMcpPort.txt（格式「端口 TAB 实例标识」，标识只给桥自己用）。
配置端口偶尔会被历史残留的 socket 占住（Unity 子进程继承句柄，或域重载后残留的监听实例），
这时桥会顺延到下一个可用端口，读这个文件就不用改客户端配置。文件不存在或内容异常时退回 -Port 参数。
#>
function Get-BridgeEndpoint {
    $resolved = $Port
    if (Test-Path -LiteralPath $ActivePortFile) {
        $text = (Get-Content -LiteralPath $ActivePortFile -Raw -ErrorAction SilentlyContinue)
        if ($text -match '^\s*(\d{4,5})\b') { $resolved = [int]$Matches[1] }
    }
    return "http://127.0.0.1:$resolved/"
}

function Write-McpMessage {
    param([Parameter(Mandatory = $true)]$Message)
    [Console]::Out.WriteLine(($Message | ConvertTo-Json -Depth 40 -Compress))
    [Console]::Out.Flush()
}

function New-JsonRpcResponse {
    param($Id, $Result)
    return [ordered]@{ jsonrpc = "2.0"; id = $Id; result = $Result }
}

function New-JsonRpcError {
    param($Id, [int]$Code, [string]$Message)
    return [ordered]@{
        jsonrpc = "2.0"
        id = $Id
        error = [ordered]@{ code = $Code; message = $Message }
    }
}

function Invoke-UnityBridge {
    param([string]$Action, $Arguments)

    $request = [ordered]@{
        action = $Action
        expectedProjectPath = $ProjectPath
    }
    if ($null -ne $Arguments) {
        foreach ($property in $Arguments.PSObject.Properties) {
            $request[$property.Name] = $property.Value
        }
    }

    $body = $request | ConvertTo-Json -Depth 30 -Compress
    # 每次都重新解析端口：Unity 域重载后桥可能换了端口。
    return Invoke-RestMethod -Uri (Get-BridgeEndpoint) -Method Post -Body $body `
        -ContentType "application/json; charset=utf-8" -TimeoutSec $TimeoutSeconds
}

<#
工具表来自 Unity（action=mcp.tools），和处理函数同居一处。
Unity 没开时退回 Library\PrefabMcpTools.json —— 那是 Unity 每次域重载导出的同一份内容；
没有它客户端会以为这个服务一个工具都没有，比报错更难排查。
#>
function Get-ToolDefinitions {
    $definitions = $null
    try {
        $response = Invoke-UnityBridge -Action "mcp.tools" -Arguments $null
        if ($null -ne $response.tools) { $definitions = @($response.tools) }
    }
    catch {
        [Console]::Error.WriteLine("[unity-prefab-mcp] bridge unavailable, serving cached tool list: $($_.Exception.Message)")
    }
    if ($null -eq $definitions -and (Test-Path -LiteralPath $ToolCacheFile)) {
        $definitions = @((Get-Content -LiteralPath $ToolCacheFile -Raw | ConvertFrom-Json))
    }
    if ($null -eq $definitions) {
        throw "Tool list unavailable: Unity is not running and $ToolCacheFile does not exist. Open this project in Unity once."
    }
    if ($SourceToolNames.Count -eq 0) {
        return $definitions
    }
    return @($definitions | Where-Object { -not $SourceToolNames.Contains([string]$_.name) })
}

function Get-ToolCacheStamp {
    if (Test-Path -LiteralPath $ToolCacheFile) {
        return [System.IO.File]::GetLastWriteTimeUtc($ToolCacheFile)
    }
    return [datetime]::MinValue
}

<#
通用等待：请求带 waitSeconds、响应带 retryAfterSeconds，就在预算内重发。是否终态由服务端说，
这层不认识具体工具，别的工具要等也不用改这里。等待只能在客户端：编译会域重载，把桥的线程一起卸掉，
因此重发期间连不上/超时同样算可重试，只有预算耗尽才报出最后一次失败。
#>
function Invoke-UnityBridgeWaiting {
    param([string]$Action, $Arguments)

    $budget = 0
    if ($null -ne $Arguments -and $null -ne $Arguments.waitSeconds) {
        $budget = [double]$Arguments.waitSeconds
    }
    $deadline = (Get-Date).AddSeconds($budget)

    while ($true) {
        try {
            $response = Invoke-UnityBridge -Action $Action -Arguments $Arguments
        }
        catch {
            if ($budget -gt 0 -and (Get-Date) -lt $deadline) {
                Start-Sleep -Milliseconds 800
                continue
            }
            throw
        }

        if ($null -eq $response.retryAfterSeconds -or (Get-Date) -ge $deadline) { return $response }
        $delay = [double]$response.retryAfterSeconds
        if ($delay -lt 0.2) { $delay = 0.2 }
        $remaining = ($deadline - (Get-Date)).TotalSeconds
        if ($remaining -le 0) { return $response }
        Start-Sleep -Milliseconds ([int]([Math]::Min($delay, $remaining) * 1000))
    }
}

function Invoke-McpTool {
    param([string]$Name, $Arguments)

    try {
        $response = Invoke-UnityBridgeWaiting -Action $Name -Arguments $Arguments
    }
    catch {
        return [ordered]@{
            content = @(@{ type = "text"; text = "Unity bridge request failed. Keep this project open in Unity and wait for script compilation to finish. If every call times out, check the Unity Console for [UnityMcp], or Tools > Unity MCP > 状态, and restart the bridge or the Editor. $($_.Exception.Message)" })
            isError = $true
        }
    }

    # 服务端已按需裁剪（空字段不上线），这里原样转发即可。error 非空即失败。
    if ($null -ne $response.error) {
        return [ordered]@{
            content = @(@{ type = "text"; text = ($response | ConvertTo-Json -Depth 30 -Compress) })
            isError = $true
        }
    }

    # 图片响应是 MCP 协议要求的另一种 content 类型，按字段存在与否判断，不用按工具名硬编码。
    if ($null -ne $response.imageBase64) {
        $meta = [ordered]@{
            message = $response.message
            width = $response.imageWidth
            height = $response.imageHeight
        }
        foreach ($name in @("coordinateSystem", "screenWidth", "screenHeight", "uiElements")) {
            if ($null -ne $response.$name) { $meta[$name] = $response.$name }
        }
        return [ordered]@{
            content = @(
                @{ type = "image"; data = $response.imageBase64; mimeType = $response.imageMimeType },
                @{ type = "text"; text = ($meta | ConvertTo-Json -Depth 10 -Compress) }
            )
            isError = $false
        }
    }

    return [ordered]@{
        content = @(@{ type = "text"; text = ($response | ConvertTo-Json -Depth 30 -Compress) })
        isError = $false
    }
}

<#
自热重载：本脚本是客户端拉起的长驻进程，改完文件后进程里仍是旧函数定义，而客户端没有"重启服务端"的请求。
每次请求前比对自身 mtime，变了就重新 dot-source。必须在脚本作用域做：在函数里 dot-source 只写进函数作用域。
被 dot-source 的那份靠 $SelfReloading 提前 return，避免递归进主循环。
#>
$SelfLoadedAtUtc = [System.IO.File]::GetLastWriteTimeUtc($PSCommandPath)
if ($SelfReloading) { return }

$ToolCacheStamp = Get-ToolCacheStamp

while ($null -ne ($line = [Console]::In.ReadLine())) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    # 从 PowerShell 灌 stdin 时首行会带 BOM，不剥掉则第一条请求必然 Parse error。
    $line = $line.TrimStart([char]0xFEFF)

    $currentWriteUtc = [System.IO.File]::GetLastWriteTimeUtc($PSCommandPath)
    if ($currentWriteUtc -ne $SelfLoadedAtUtc) {
        # dot-source 会重跑 param 块把实参覆盖成默认值，重载后还回去。
        $savedPort = $Port
        $savedTimeout = $TimeoutSeconds
        $savedProjectPath = $ProjectPath
        $savedIncludeSourceCodeTools = $IncludeSourceCodeTools
        $SelfReloading = $true
        try {
            . $PSCommandPath
        }
        catch {
            # stdout 只能放 JSON-RPC；重载失败继续用旧定义，原因走 stderr。
            [Console]::Error.WriteLine("[unity-prefab-mcp] self-reload failed, keeping previous version: $($_.Exception.Message)")
        }
        $SelfReloading = $false
        $Port = $savedPort
        $TimeoutSeconds = $savedTimeout
        $ProjectPath = $savedProjectPath
        $IncludeSourceCodeTools = $savedIncludeSourceCodeTools
        $ActivePortFile = Join-Path $ProjectPath "Library\PrefabMcpPort.txt"
        $ToolCacheFile = Join-Path $ProjectPath "Library\PrefabMcpTools.json"
        $SourceToolFile = Join-Path $ProjectPath $SourceToolRelativePath
        Set-SourceToolFilter
        $SelfLoadedAtUtc = $currentWriteUtc
    }

    # 工具表在 Unity 侧，schema 改了会重新导出这个文件；比对 mtime 就能知道要不要通知客户端。
    # 只比本地文件、不额外请求 Unity，代价基本为零。
    $currentToolStamp = Get-ToolCacheStamp
    if ($currentToolStamp -ne $ToolCacheStamp) {
        $ToolCacheStamp = $currentToolStamp
        Write-McpMessage ([ordered]@{ jsonrpc = "2.0"; method = "notifications/tools/list_changed" })
    }

    try {
        $request = $line | ConvertFrom-Json
    }
    catch {
        Write-McpMessage (New-JsonRpcError -Id $null -Code -32700 -Message "Parse error")
        continue
    }

    $id = $request.id
    try {
        switch ($request.method) {
            "initialize" {
                $requestedVersion = $request.params.protocolVersion
                $version = if ($requestedVersion) { $requestedVersion } else { "2024-11-05" }
                $result = [ordered]@{
                    protocolVersion = $version
                    capabilities = [ordered]@{ tools = [ordered]@{ listChanged = $true } }
                    serverInfo = [ordered]@{ name = "unity-prefab-mcp"; version = "0.7.0" }
                }
                Write-McpMessage (New-JsonRpcResponse -Id $id -Result $result)
            }
            "notifications/initialized" { }
            "ping" {
                Write-McpMessage (New-JsonRpcResponse -Id $id -Result ([ordered]@{}))
            }
            "tools/list" {
                Write-McpMessage (New-JsonRpcResponse -Id $id -Result ([ordered]@{ tools = @(Get-ToolDefinitions) }))
            }
            "tools/call" {
                $result = Invoke-McpTool -Name $request.params.name -Arguments $request.params.arguments
                Write-McpMessage (New-JsonRpcResponse -Id $id -Result $result)
            }
            default {
                if ($null -ne $id) {
                    Write-McpMessage (New-JsonRpcError -Id $id -Code -32601 -Message "Method not found: $($request.method)")
                }
            }
        }
    }
    catch {
        if ($null -ne $id) {
            Write-McpMessage (New-JsonRpcError -Id $id -Code -32603 -Message $_.Exception.Message)
        }
    }
}
