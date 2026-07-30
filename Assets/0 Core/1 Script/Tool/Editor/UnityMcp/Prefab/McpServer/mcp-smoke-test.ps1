<#
.SYNOPSIS
    对 unity-prefab-mcp.ps1 跑一遍真实的 JSON-RPC 冒烟测试。

.DESCRIPTION
    另起服务端进程、按 MCP 协议灌 stdin、逐条校验响应，改完 ps1 不必重启 MCP 客户端即可验证。
    大部分用例只读或 dryRun=true；事务协议包含一次可恢复写测试，finally 中优先走 MCP 备份还原，
    再以测试前的原始字节兜底，并校验 Prefab SHA-256 完全一致。
    需要 Unity 打开本项目并完成编译。

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
$missingPrefabPath = "Assets/__InspectorBridgeSmoke__/DryRun-$([Guid]::NewGuid().ToString('N')).prefab"

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
$treeRequestId = $nextId
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
Add-Request -Method "tools/call" -Label "edit_prefab(dry-run 建节点 + `$1 引用它)" -Params ([ordered]@{
    name = "edit_prefab"
    arguments = [ordered]@{
        prefabPath = $PrefabPath; dryRun = $true
        operations = @(
            [ordered]@{ op = "createObject"; parentObjectId = "0"; newName = "SmokeTestNode" },
            [ordered]@{ op = "rename"; objectId = "`$1"; newName = "SmokeTestRenamed" }
        )
    }
})
Add-Request -Method "tools/call" -Label "edit_prefab(expectedRevision 冲突)" -ExpectError -Params ([ordered]@{
    name = "edit_prefab"
    arguments = [ordered]@{
        prefabPath = $PrefabPath
        dryRun = $true
        expectedRevision = "definitely-stale"
        operations = @(
            [ordered]@{ op = "setSiblingIndex"; objectId = "0"; siblingIndex = "0" }
        )
    }
})
$componentRequestId = $nextId
Add-Request -Method "tools/call" -Label "edit_prefab(新 Prefab + 组件内联配置/稳定引用)" -Params ([ordered]@{
    name = "edit_prefab"
    arguments = [ordered]@{
        prefabPath = $missingPrefabPath
        dryRun = $true
        createIfMissing = [ordered]@{ rootName = "SmokeRoot"; transformType = "RectTransform" }
        operations = @(
            [ordered]@{
                op = "ensureComponent"; objectId = "0"; componentType = "CanvasGroup"
                componentRef = "smokeGroup"; setIfDifferent = $true
                values = [ordered]@{ m_Alpha = "0.5"; m_Interactable = "true" }
            },
            [ordered]@{
                op = "addComponent"; objectId = "0"; componentType = "UnityEngine.UI.Image"
                componentRef = "smokeImage"; values = [ordered]@{ m_Color = "#336699FF" }
            },
            [ordered]@{
                op = "addComponent"; objectId = "0"; componentType = "UnityEngine.UI.Button"
                componentRef = "smokeButton"
                values = [ordered]@{ m_TargetGraphic = "component:smokeImage" }
            },
            [ordered]@{
                op = "setValue"; componentRef = "smokeGroup"; propertyPath = "m_Alpha"
                value = "0.75"; setIfDifferent = $true
            },
            [ordered]@{
                op = "setValues"; objectId = "0"; componentType = "CanvasGroup"; setIfDifferent = $true
                values = [ordered]@{ m_Alpha = "0.75"; m_BlocksRaycasts = "false" }
            },
            [ordered]@{
                op = "ensureComponent"; objectId = "0"; componentType = "CanvasGroup"
                componentRef = "sameGroup"; setIfDifferent = $true
                values = [ordered]@{ m_Alpha = "0.75" }
            }
        )
    }
})
Add-Request -Method "tools/call" -Label "edit_prefab(createIfMissing 拒绝路径穿越)" -ExpectError -Params ([ordered]@{
    name = "edit_prefab"
    arguments = [ordered]@{
        prefabPath = "Assets/../Escaped.prefab"
        dryRun = $true
        createIfMissing = [ordered]@{ rootName = "Escaped" }
        operations = @([ordered]@{ op = "rename"; objectId = "0"; newName = "Escaped" })
    }
})
# waitSeconds 走的是「响应带 retryAfterSeconds 就重发」的通用等待：结果已新鲜时必须立刻返回，不能空等预算。
Add-Request -Method "tools/call" -Label "get_unity_compile_status(waitSeconds 不空等)" -Params ([ordered]@{
    name = "get_unity_compile_status"
    arguments = [ordered]@{ excludeMessages = $true; waitSeconds = 5 }
})
# 按类型删组件：类型不在该节点上时要报错并列出现有组件。
Add-Request -Method "tools/call" -Label "edit_prefab(removeComponent 按类型找不到)" -ExpectError -Params ([ordered]@{
    name = "edit_prefab"
    arguments = [ordered]@{
        prefabPath = $PrefabPath; dryRun = $true
        operations = @([ordered]@{ op = "removeComponent"; objectId = "0"; componentType = "AudioSource" })
    }
})
Add-Request -Method "tools/call" -Label "edit_prefab(必然失败的 objectId，验证事务中止)" -ExpectError -Params ([ordered]@{
    name = "edit_prefab"
    arguments = [ordered]@{
        prefabPath = $PrefabPath; dryRun = $true
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

function Invoke-McpToolCall {
    param(
        [int]$Id,
        [string]$Name,
        $Arguments,
        [int]$Timeout = 40
    )

    $child = [System.Diagnostics.Process]::Start($psi)
    try {
        $writer = New-Object System.IO.StreamWriter(
            $child.StandardInput.BaseStream, (New-Object System.Text.UTF8Encoding $false))
        $initialize = [ordered]@{
            jsonrpc = "2.0"; id = -1; method = "initialize"
            params = [ordered]@{
                protocolVersion = "2024-11-05"; capabilities = @{}
                clientInfo = [ordered]@{ name = "mcp-smoke-transaction"; version = "1.0" }
            }
        }
        $call = [ordered]@{
            jsonrpc = "2.0"; id = $Id; method = "tools/call"
            params = [ordered]@{ name = $Name; arguments = $Arguments }
        }
        $writer.WriteLine(($initialize | ConvertTo-Json -Depth 30 -Compress))
        $writer.WriteLine(($call | ConvertTo-Json -Depth 30 -Compress))
        $writer.Flush()
        $writer.Close()

        $deadline = (Get-Date).AddSeconds($Timeout)
        while ((Get-Date) -lt $deadline) {
            $line = $child.StandardOutput.ReadLine()
            if ($null -eq $line) { break }
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $message = $line | ConvertFrom-Json
            if ($null -ne $message.id -and [int]$message.id -eq $Id) { return $message }
        }
        return $null
    }
    finally {
        if (-not $child.HasExited) { $child.Kill() }
        $child.Dispose()
    }
}

function Get-McpPayload {
    param($Message)
    if ($null -eq $Message) { throw "MCP 调用超时" }
    if ($null -ne $Message.error) {
        throw "JSON-RPC error $($Message.error.code): $($Message.error.message)"
    }
    $text = ($Message.result.content | Where-Object { $_.type -eq "text" } | Select-Object -First 1).text
    if ([string]::IsNullOrWhiteSpace($text)) { throw "MCP 工具没有返回文本 payload" }
    if ([bool]$Message.result.isError) { throw $text }
    return ($text | ConvertFrom-Json)
}

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

$dynamicPassed = 0
try {
    $componentPayload = Get-McpPayload $responses[[int]$componentRequestId]
    $aliases = @($componentPayload.edit.results | ForEach-Object { $_.alias })
    foreach ($expectedAlias in @("smokeGroup", "smokeImage", "smokeButton", "sameGroup")) {
        if ($aliases -notcontains $expectedAlias) {
            throw "summary 响应缺少组件别名/句柄: $expectedAlias"
        }
    }
    if (@($componentPayload.edit.results | Where-Object { $_.op -eq "setValue" -or $_.op -eq "setValues" }).Count -gt 0) {
        throw "summary 响应未裁掉普通 setValue/setValues 明细"
    }
    Write-Host "[PASS] edit_prefab(summary 保留组件句柄且裁掉普通字段明细)" -ForegroundColor Green
    $dynamicPassed++
}
catch {
    Write-Host ("[FAIL] edit_prefab summary 句柄校验 -> {0}" -f $_.Exception.Message) -ForegroundColor Red
    $failed++
}

try {
    $treePayload = Get-McpPayload $responses[[int]$treeRequestId]
    $nodes = @($treePayload.nodes)
    $rootNode = $nodes | Where-Object { $_.objectId -eq "0" } | Select-Object -First 1
    if ($null -eq $rootNode) { throw "get_prefab_tree 未返回根节点" }
    $childNode = $nodes | Where-Object { $_.objectId -ne "0" } | Select-Object -First 1
    if ($null -ne $childNode) {
        $segments = $childNode.objectId -split '/'
        $planOperation = [ordered]@{
            op = "setSiblingIndex"
            objectId = $childNode.objectId
            siblingIndex = $segments[$segments.Length - 1]
        }
    }
    else {
        $planOperation = [ordered]@{
            op = "rename"
            objectId = "0"
            newName = $rootNode.name
        }
    }

    $prefabFullPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $PrefabPath))
    $originalBytes = [IO.File]::ReadAllBytes($prefabFullPath)
    $hashBefore = (Get-FileHash -LiteralPath $prefabFullPath -Algorithm SHA256).Hash
    $commitBackupPath = $null
    $cleanupError = $null
    $fallbackUsed = $false
    try {
        $planMessage = Invoke-McpToolCall -Id 1001 -Name "edit_prefab" -Arguments ([ordered]@{
            prefabPath = $PrefabPath
            dryRun = $true
            operations = @($planOperation)
        }) -Timeout $TimeoutSeconds
        $planPayload = Get-McpPayload $planMessage
        $planId = $planPayload.edit.planId
        if ([string]::IsNullOrWhiteSpace($planId)) { throw "dryRun 响应没有 planId" }
        Write-Host ("[PASS] edit_prefab(dryRun -> planId) {0}" -f $planId) -ForegroundColor Green
        $dynamicPassed++

        $idempotencyKey = "smoke-plan-$([Guid]::NewGuid().ToString('N'))"
        $commitArguments = [ordered]@{
            planId = $planId
            idempotencyKey = $idempotencyKey
            responseMode = "full"
        }
        $commitPayload = Get-McpPayload (Invoke-McpToolCall -Id 1002 -Name "edit_prefab" `
            -Arguments $commitArguments -Timeout $TimeoutSeconds)
        $commitBackupPath = $commitPayload.edit.backupPath
        if ($commitPayload.edit.applied -ne $true) { throw "plan 提交未返回 applied=true" }
        Write-Host "[PASS] edit_prefab(planId 幂等提交)" -ForegroundColor Green
        $dynamicPassed++

        $replayPayload = Get-McpPayload (Invoke-McpToolCall -Id 1003 -Name "edit_prefab" `
            -Arguments ([ordered]@{
                planId = $planId
                idempotencyKey = $idempotencyKey
                responseMode = "summary"
            }) -Timeout $TimeoutSeconds)
        if ($replayPayload.edit.replayed -ne $true) { throw "同键重试未返回 replayed=true" }
        Write-Host "[PASS] edit_prefab(同键重放且不重复执行)" -ForegroundColor Green
        $dynamicPassed++

        $conflictMessage = Invoke-McpToolCall -Id 1004 -Name "edit_prefab" -Arguments ([ordered]@{
            planId = $planId
            prefabPath = $PrefabPath
            idempotencyKey = $idempotencyKey
        }) -Timeout $TimeoutSeconds
        if ($null -eq $conflictMessage -or -not [bool]$conflictMessage.result.isError) {
            throw "同一个 idempotencyKey 用于不同请求时没有被拒绝"
        }
        $conflictText = ($conflictMessage.result.content |
            Where-Object { $_.type -eq "text" } | Select-Object -First 1).text
        $conflictPayload = $conflictText | ConvertFrom-Json
        if ($conflictPayload.errorCode -ne "IDEMPOTENCY_KEY_CONFLICT") {
            throw "同键异请求错误码不正确: $($conflictPayload.errorCode)"
        }
        Write-Host "[PASS] edit_prefab(同键异请求冲突)" -ForegroundColor Green
        $dynamicPassed++
    }
    finally {
        if (-not [string]::IsNullOrWhiteSpace($commitBackupPath)) {
            try {
                [void](Get-McpPayload (Invoke-McpToolCall -Id 1005 -Name "restore_prefab_backup" `
                    -Arguments ([ordered]@{
                        prefabPath = $PrefabPath
                        backupPath = $commitBackupPath
                        listOnly = $false
                    }) -Timeout $TimeoutSeconds))
                Write-Host "[PASS] restore_prefab_backup(finally 恢复 plan 提交前内容)" -ForegroundColor Green
                $dynamicPassed++
            }
            catch {
                $cleanupError = "自动备份还原失败: $($_.Exception.Message)"
            }
        }

        $hashAfter = (Get-FileHash -LiteralPath $prefabFullPath -Algorithm SHA256).Hash
        if ($hashAfter -ne $hashBefore) {
            [IO.File]::WriteAllBytes($prefabFullPath, $originalBytes)
            $fallbackUsed = $true
            try {
                [void](Get-McpPayload (Invoke-McpToolCall -Id 1006 -Name "refresh_unity_assets" `
                    -Arguments ([ordered]@{ refreshOnly = $true }) -Timeout $TimeoutSeconds))
            }
            catch {
                $cleanupError = ($cleanupError + "; 字节兜底后的 Unity Refresh 失败: " +
                    $_.Exception.Message).TrimStart(';', ' ')
            }
            $hashAfter = (Get-FileHash -LiteralPath $prefabFullPath -Algorithm SHA256).Hash
        }
        if ($hashAfter -ne $hashBefore) {
            throw "事务冒烟后 Prefab 无法恢复：before=$hashBefore after=$hashAfter"
        }
        if ($fallbackUsed) {
            $cleanupError = ($cleanupError + "; MCP 还原未恢复原 hash，已用测试前字节安全兜底").TrimStart(';', ' ')
        }
        if (-not [string]::IsNullOrWhiteSpace($cleanupError)) {
            throw $cleanupError
        }
    }
}
catch {
    Write-Host ("[FAIL] edit_prefab 动态事务链 -> {0}" -f $_.Exception.Message) -ForegroundColor Red
    $failed++
}

if ($notifications.Count -gt 0) {
    Write-Host ("收到通知：{0}" -f ($notifications -join ", ")) -ForegroundColor DarkGray
}
if ($stderr) {
    Write-Host "服务端 stderr：" -ForegroundColor Yellow
    Write-Host $stderr
}

if ($failed -gt 0) {
    Write-Host ("{0}/{1} 个静态用例失败（动态通过 {2}）" -f $failed, $requests.Count, $dynamicPassed) -ForegroundColor Red
    exit 1
}
Write-Host ("全部 {0} 个静态用例 + {1} 个动态事务用例通过" -f $requests.Count, $dynamicPassed) -ForegroundColor Green
