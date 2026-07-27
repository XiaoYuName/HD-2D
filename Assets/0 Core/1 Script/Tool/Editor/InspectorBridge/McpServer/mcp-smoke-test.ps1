<#
.SYNOPSIS
    对 unity-prefab-mcp.ps1 跑一遍真实的 JSON-RPC 冒烟测试。

.DESCRIPTION
    另起服务端进程、按 MCP 协议灌 stdin、逐条校验响应，改完 ps1 不必重启 MCP 客户端即可验证。
    全部用例只读或 apply=false 预演，不写入任何 Prefab。需要 Unity 打开本项目并完成编译。

.EXAMPLE
    powershell -NoProfile -File mcp-smoke-test.ps1
    powershell -NoProfile -File mcp-smoke-test.ps1 -PrefabPath "Assets/.../XxxPanel.prefab"
#>
param(
    [string]$PrefabPath = "",
    [int]$TimeoutSeconds = 40
)

$ErrorActionPreference = "Stop"
$serverScript = Join-Path $PSScriptRoot "unity-prefab-mcp.ps1"

# 服务端自己会向上找项目根，这里只是为了给默认 PrefabPath 兜底时能搜到资源。
$cursor = Get-Item -LiteralPath $PSScriptRoot
# 和服务端同样要求 Assets + ProjectSettings 同时存在：项目里可能有名叫 Assets/ProjectSettings 的资源目录。
while ($null -ne $cursor -and
    (-not (Test-Path -LiteralPath (Join-Path $cursor.FullName "Assets")) -or
     -not (Test-Path -LiteralPath (Join-Path $cursor.FullName "ProjectSettings")))) {
    $cursor = $cursor.Parent
}
if ($null -eq $cursor) { throw "找不到 Unity 项目根目录" }
$projectRoot = $cursor.FullName

if ([string]::IsNullOrWhiteSpace($PrefabPath)) {
    $found = Get-ChildItem -LiteralPath (Join-Path $projectRoot "Assets") -Filter *.prefab -Recurse -File |
        Sort-Object Length -Descending | Select-Object -First 1
    if ($null -eq $found) { throw "Assets 下没有找到任何 .prefab，请用 -PrefabPath 指定" }
    $PrefabPath = ($found.FullName.Substring($projectRoot.Length + 1)) -replace '\\', '/'
    Write-Host "未指定 -PrefabPath，自动选用体积最大的 Prefab：$PrefabPath" -ForegroundColor DarkGray
}

$requests = New-Object System.Collections.ArrayList
$nextId = 1
function Add-Request {
    param([string]$Method, $Params, [string]$Label, [switch]$ExpectError)
    $id = $script:nextId
    $script:nextId++
    $payload = [ordered]@{ jsonrpc = "2.0"; id = $id; method = $Method }
    if ($null -ne $Params) { $payload.params = $Params }
    [void]$script:requests.Add([ordered]@{
        id = $id
        label = $Label
        expectError = [bool]$ExpectError
        json = ($payload | ConvertTo-Json -Depth 30 -Compress)
    })
}

Add-Request -Method "initialize" -Label "initialize" -Params ([ordered]@{
    protocolVersion = "2024-11-05"; capabilities = @{}
    clientInfo = [ordered]@{ name = "mcp-smoke-test"; version = "1.0" }
})
Add-Request -Method "tools/list" -Label "tools/list" -Params $null
Add-Request -Method "tools/call" -Label "unity_prefab_status" -Params ([ordered]@{
    name = "unity_prefab_status"; arguments = @{}
})
Add-Request -Method "tools/call" -Label "get_prefab_tree(compact 默认)" -Params ([ordered]@{
    name = "get_prefab_tree"
    arguments = [ordered]@{ prefabPath = $PrefabPath; maxDepth = 3; maxResults = 60 }
})
Add-Request -Method "tools/call" -Label "get_component_fields(targets 批量)" -Params ([ordered]@{
    name = "get_component_fields"
    arguments = [ordered]@{ prefabPath = $PrefabPath; targets = @([ordered]@{ objectId = "0"; componentIndex = -1 }) }
})
Add-Request -Method "tools/call" -Label "validate_prefab" -Params ([ordered]@{
    name = "validate_prefab"; arguments = [ordered]@{ prefabPath = $PrefabPath }
})
Add-Request -Method "tools/call" -Label "edit_prefab(dry-run 建节点 + \$1 引用它)" -Params ([ordered]@{
    name = "edit_prefab"
    arguments = [ordered]@{
        prefabPath = $PrefabPath; apply = $false
        operations = @(
            [ordered]@{ op = "createObject"; parentObjectId = "0"; newName = "SmokeTestNode" },
            [ordered]@{ op = "rename"; objectId = "`$1"; newName = "SmokeTestRenamed" }
        )
    }
})
Add-Request -Method "tools/call" -Label "edit_prefab(必然失败的 objectId，验证事务中止)" -ExpectError -Params ([ordered]@{
    name = "edit_prefab"
    arguments = [ordered]@{
        prefabPath = $PrefabPath; apply = $false
        operations = @([ordered]@{ op = "rename"; objectId = "0/999/999"; newName = "SmokeTest" })
    }
})
# 参数名拼错必须显式报错，而不是静默当默认值处理（服务端 MissingMemberHandling.Error）。
Add-Request -Method "tools/call" -Label "get_prefab_tree(拼错参数名，验证严格校验)" -ExpectError -Params ([ordered]@{
    name = "get_prefab_tree"
    arguments = [ordered]@{ prefabPath = $PrefabPath; maxDepht = 3 }
})

# 用 MCP 客户端实际使用的解释器（pwsh），PATH 里没有就退回已知安装路径，最后才用 5.1。
function Resolve-Interpreter {
    $onPath = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    foreach ($variable in @("ProgramFiles", "ProgramW6432")) {
        $root = [Environment]::GetEnvironmentVariable($variable)
        if (-not $root) { continue }
        $found = Get-ChildItem -LiteralPath (Join-Path $root "PowerShell") -Filter pwsh.exe -Recurse -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    Write-Host "未找到 pwsh，退回 Windows PowerShell 5.1（和客户端实际运行环境不一致）" -ForegroundColor Yellow
    return (Get-Command powershell.exe).Source
}

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = Resolve-Interpreter
$psi.Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$serverScript`""
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.StandardOutputEncoding = New-Object System.Text.UTF8Encoding $false
$psi.StandardErrorEncoding = New-Object System.Text.UTF8Encoding $false

$process = [System.Diagnostics.Process]::Start($psi)
$responses = @{}
$notifications = New-Object System.Collections.ArrayList
try {
    # .NET Framework 没有 StandardInputEncoding，默认跟随宿主的带 BOM UTF8，首行会被写上 BOM。
    $writer = New-Object System.IO.StreamWriter($process.StandardInput.BaseStream, (New-Object System.Text.UTF8Encoding $false))
    foreach ($request in $requests) { $writer.WriteLine($request.json) }
    $writer.Flush()
    $writer.Close()

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ($responses.Count -lt $requests.Count -and (Get-Date) -lt $deadline) {
        $line = $process.StandardOutput.ReadLine()
        if ($null -eq $line) { break }
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $message = $line | ConvertFrom-Json
        if ($null -eq $message.id) { [void]$notifications.Add($message.method); continue }
        $responses[[int]$message.id] = $message
    }
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
    $stderr = $process.StandardError.ReadToEnd()
    $process.Dispose()
}

$failed = 0
foreach ($request in $requests) {
    $message = $responses[[int]$request.id]
    if ($null -eq $message) {
        Write-Host ("[超时] {0}" -f $request.label) -ForegroundColor Red
        $failed++
        continue
    }
    if ($null -ne $message.error) {
        Write-Host ("[FAIL] {0} -> JSON-RPC error {1}: {2}" -f $request.label, $message.error.code, $message.error.message) -ForegroundColor Red
        $failed++
        continue
    }

    $detail = ""
    $size = 0
    if ($null -ne $message.result.content) {
        $text = ($message.result.content | Where-Object { $_.type -eq "text" } | Select-Object -First 1).text
        $size = 0
        if ($text) { $size = $text.Length }
        # 部分用例的预期结果就是 isError=true（事务中止、参数校验失败）。
        $expectError = [bool]$request.expectError
        if ([bool]$message.result.isError -ne $expectError) {
            Write-Host ("[FAIL] {0} -> isError={1}，期望 {2}；{3}" -f $request.label, $message.result.isError, $expectError, $text) -ForegroundColor Red
            $failed++
            continue
        }
        $parsed = $null
        try { $parsed = $text | ConvertFrom-Json } catch { }
        if ($null -ne $parsed.message) { $detail = $parsed.message }
        elseif ($null -ne $parsed.error) { $detail = $parsed.error }
    }
    elseif ($null -ne $message.result.tools) {
        $detail = "{0} 个工具" -f @($message.result.tools).Count
    }
    elseif ($null -ne $message.result.serverInfo) {
        $detail = "{0} {1}，tools.listChanged={2}" -f $message.result.serverInfo.name,
            $message.result.serverInfo.version, $message.result.capabilities.tools.listChanged
    }

    $sizeText = ""
    if ($size -gt 0) { $sizeText = " [{0} 字符]" -f $size }
    Write-Host ("[PASS] {0}{1} {2}" -f $request.label, $sizeText, $detail) -ForegroundColor Green
}

if ($notifications.Count -gt 0) {
    Write-Host ("收到通知：{0}" -f ($notifications -join ", ")) -ForegroundColor DarkGray
}
if ($stderr) {
    Write-Host "服务端 stderr：" -ForegroundColor Yellow
    Write-Host $stderr
}

if ($failed -gt 0) {
    Write-Host ("{0}/{1} 个用例失败" -f $failed, $requests.Count) -ForegroundColor Red
    exit 1
}
Write-Host ("全部 {0} 个用例通过" -f $requests.Count) -ForegroundColor Green
