# 多语言 CSV 行级增/删/改/查（不依赖 Unity 编辑器，纯文本操作，可在 Editor 打开工程时安全并行使用）。
# 解析/拼行规则与 Assets/0 Core/1 Script/Tool/Localization/Editor/LocCsvEditor.cs、LocCsvMerger.cs 保持一致：
#   - 表头沿用 Key,Id,语言列("语言名(代码)")；Key/非空语言列加引号，Id 与空语言列留空不加引号。
#   - 引号转义：内部 " 变 ""；保留原文件 BOM 与换行风格。
# 单条用法（务必用 & 调用操作符直接调脚本，同一进程内解析参数数组；
#      不要套一层 `powershell -File ...`，多进程转发命令行会打乱带引号/逗号的值）：
#   & Tools/LocCsv.ps1 -Action Add    -Csv <相对/绝对路径> -Key <Key> -Set 'code=值','code=值',...
#   & Tools/LocCsv.ps1 -Action Update -Csv <路径> -Key <Key> -Set 'code=值',...      # 只改指定语言列，其余列保持原值
#   & Tools/LocCsv.ps1 -Action Remove -Csv <路径> -Key <Key>
#   & Tools/LocCsv.ps1 -Action Get    -Csv <路径> -Key <Key>                        # 只打印该 Key 各语言值，不用整份读文件
# -Set 多个语言必须作为一个数组传（同一个 -Set 后跟逗号分隔的多个值），不能重复写多个 -Set。
#
# 批量用法（一次要加/删多条时用这个，别手写循环反复起进程调用）：
#   & Tools/LocCsv.ps1 -Action Batch -File <JSON 文件路径>
# JSON 是一个数组，每项一条操作，形如：
#   [
#     { "action": "Remove", "csv": "Assets/.../FactoryProcessPanel.csv", "key": "FactoryProcOldKey" },
#     { "action": "Add",    "csv": "Assets/.../FactoryProcessPanel.csv", "key": "FactoryProcNewKey",
#       "set": { "zh-CN": "中文", "zh-TW": "繁中", "en": "English", "ja-JP": "日本語", "ko": "한국어", "th": "ไทย", "vi": "Việt" } },
#     { "action": "Update", "csv": "...", "key": "SomeKey", "set": { "en": "Only change English" } }
#   ]
# JSON 文件本身用什么工具写都行（含中日韩泰越等非 ASCII 字符也没问题），因为 Batch 模式用 -Encoding UTF8 显式读取该文件的文本内容，
# 不依赖 PowerShell 判断文件有没有 BOM ——这一点跟直接把一堆 `& LocCsv.ps1 ...` 调用堆进临时 .ps1 驱动脚本不一样：
# 那种驱动脚本是被 PowerShell 当"脚本源码"解析的，若没有 UTF-8 BOM，Windows PowerShell 5.1 会按系统默认代码页读取源码文本，
# 脚本里写的非 ASCII 字面量会被读错、乱码写进 CSV。用 Batch + JSON 从机制上就不会踩这个坑，优先用它做批量操作。
# 单条失败不会中断整批，会在输出里标 FAIL 并继续跑完剩余项，最后汇总成功/失败数量（有失败时进程退出码为 1）。
#
# 注意：本脚本只改 CSV 源文件；要让改动在游戏里生效，仍需在 Unity 编辑器里跑一次对应的导入菜单
#      （如 Tools/工厂小游戏/导入「工厂」全部多语言 → Factory 表），把 CSV 合并进 StringTable 资产。

param(
    [Parameter(Mandatory = $true)][ValidateSet('Add', 'Remove', 'Update', 'Get', 'Batch')][string]$Action,
    [string]$Csv,
    [string]$Key,
    [string[]]$Set = @(),
    [string]$File   # -Action Batch 专用：JSON 数组文件路径
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-CsvPath([string]$path) {
    if ([System.IO.Path]::IsPathRooted($path)) { return $path }
    return (Join-Path $RepoRoot $path)
}

# 与 LocCsvMerger.ParseCsv 等价的最小 RFC4180 解析：支持引号内逗号/换行，"" 表示字面量引号
function Parse-Csv([string]$text) {
    $rows = New-Object System.Collections.Generic.List[object]
    $row = New-Object System.Collections.Generic.List[string]
    $sb = New-Object System.Text.StringBuilder
    $inQuotes = $false
    $len = $text.Length
    $i = 0
    while ($i -lt $len) {
        $ch = $text[$i]
        if ($inQuotes) {
            if ($ch -eq '"') {
                if (($i + 1) -lt $len -and $text[$i + 1] -eq '"') { [void]$sb.Append('"'); $i++ }
                else { $inQuotes = $false }
            }
            else { [void]$sb.Append($ch) }
        }
        else {
            if ($ch -eq '"') { $inQuotes = $true }
            elseif ($ch -eq ',') { [void]$row.Add($sb.ToString()); [void]$sb.Clear() }
            elseif ($ch -eq "`r") { }
            elseif ($ch -eq "`n") {
                [void]$row.Add($sb.ToString()); [void]$sb.Clear()
                [void]$rows.Add($row)
                $row = New-Object System.Collections.Generic.List[string]
            }
            else { [void]$sb.Append($ch) }
        }
        $i++
    }
    if ($sb.Length -gt 0 -or $row.Count -gt 0) { [void]$row.Add($sb.ToString()); [void]$rows.Add($row) }
    , $rows   # 逗号运算符：阻止 PowerShell 把 List 结果自动展开成 Object[]（否则调用方拿到的就不是 List，没有 GetRange 等方法）
}

# 取表头末尾括号内的语言代码，如 "Chinese (Simplified)(zh-CN)" -> "zh-CN"
function Get-LocaleCode([string]$header) {
    $open = $header.LastIndexOf('(')
    $close = $header.LastIndexOf(')')
    if ($open -ge 0 -and $close -gt $open) { return $header.Substring($open + 1, $close - $open - 1).Trim() }
    return $null
}

function Get-Columns($headerRow) {
    $cols = @()
    for ($c = 0; $c -lt $headerRow.Count; $c++) {
        $h = $headerRow[$c].Trim().TrimStart([char]0xFEFF)
        $isKey = $h -ieq 'Key'
        $isId = $h -ieq 'Id'
        $code = $null
        if (-not $isKey -and -not $isId) { $code = Get-LocaleCode $h }
        $cols += [PSCustomObject]@{ Index = $c; Header = $h; Code = $code; IsKey = $isKey; IsId = $isId }
    }
    return $cols
}

function Quote-Field($s) {
    if ($null -eq $s) { $s = '' }
    return '"' + ($s -replace '"', '""') + '"'
}

# 按列序拼一行：Key 与非空语言值加引号；Id、空语言值留空。与 LocCsvEditor.BuildRow 一致。
function Build-Row($cols, [string]$key, [hashtable]$valuesByCode, $idValue) {
    $cells = @()
    foreach ($col in $cols) {
        if ($col.IsKey) { $cells += (Quote-Field $key) }
        elseif ($col.IsId) {
            if ([string]::IsNullOrEmpty($idValue)) { $cells += '' } else { $cells += (Quote-Field $idValue) }
        }
        elseif ($col.Code -and $valuesByCode.ContainsKey($col.Code) -and -not [string]::IsNullOrEmpty($valuesByCode[$col.Code])) {
            $cells += (Quote-Field $valuesByCode[$col.Code])
        }
        else { $cells += '' }
    }
    return ($cells -join ',')
}

function Get-RowValues($cols, $row) {
    $vals = @{}
    foreach ($col in $cols) {
        if ($col.Code) { $vals[$col.Code] = if ($col.Index -lt $row.Count) { $row[$col.Index] } else { '' } }
    }
    return $vals
}

function Get-IdValue($cols, $row) {
    $idCol = $cols | Where-Object { $_.IsId } | Select-Object -First 1
    if ($null -eq $idCol) { return $null }
    if ($idCol.Index -lt $row.Count) { return $row[$idCol.Index] }
    return $null
}

# 单条操作的核心实现：不 exit/Write-Error，只回填 $result，方便单条调用与 Batch 循环共用。
function Invoke-LocOp {
    param(
        [Parameter(Mandatory = $true)][string]$OpAction,
        [Parameter(Mandatory = $true)][string]$OpCsv,
        [Parameter(Mandatory = $true)][string]$OpKey,
        [hashtable]$OpSet = @{}
    )

    $result = [PSCustomObject]@{ Success = $false; Message = ''; Values = $null }

    $fullPath = Resolve-CsvPath $OpCsv
    if (-not (Test-Path -LiteralPath $fullPath)) { $result.Message = "找不到文件：$fullPath"; return $result }

    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $content = [System.IO.File]::ReadAllText($fullPath)
    $nl = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }

    $rows = Parse-Csv $content
    if ($rows.Count -lt 1) { $result.Message = "CSV 解析失败或为空：$fullPath"; return $result }

    $cols = Get-Columns $rows[0]
    $keyCol = $cols | Where-Object { $_.IsKey } | Select-Object -First 1
    if ($null -eq $keyCol) { $result.Message = "CSV 表头缺少 Key 列：$fullPath"; return $result }
    $keyIdx = $keyCol.Index

    $dataRows = @()
    if ($rows.Count -gt 1) { $dataRows = $rows.GetRange(1, $rows.Count - 1) }

    $existingIndex = -1
    for ($r = 0; $r -lt $dataRows.Count; $r++) {
        $row = $dataRows[$r]
        if ($keyIdx -lt $row.Count -and $row[$keyIdx].Trim() -eq $OpKey) { $existingIndex = $r; break }
    }

    switch ($OpAction) {
        'Get' {
            if ($existingIndex -lt 0) { $result.Message = "NOT_FOUND: $OpKey"; return $result }
            $vals = Get-RowValues $cols $dataRows[$existingIndex]
            $lines = @()
            foreach ($col in $cols) { if ($col.Code) { $lines += "$($col.Code)=$($vals[$col.Code])" } }
            $result.Success = $true
            $result.Values = $vals
            $result.Message = ($lines -join "`n")
            return $result
        }
        'Add' {
            if ($existingIndex -ge 0) { $result.Message = "Key「$OpKey」已存在于 $(Split-Path -Leaf $fullPath)，未添加。"; return $result }
            $newLine = Build-Row $cols $OpKey $OpSet $null
            $toWrite = $content
            if ($toWrite.Length -gt 0 -and -not $toWrite.EndsWith("`n")) { $toWrite += $nl }
            $toWrite += $newLine + $nl
            [System.IO.File]::WriteAllText($fullPath, $toWrite, (New-Object System.Text.UTF8Encoding($hasBom)))
            $result.Success = $true
            $result.Message = "已向 $(Split-Path -Leaf $fullPath) 添加 Key「$OpKey」。"
            return $result
        }
        'Remove' {
            if ($existingIndex -lt 0) { $result.Message = "找不到 Key「$OpKey」于 $(Split-Path -Leaf $fullPath)。"; return $result }
            $lines = @()
            for ($r = 0; $r -lt $dataRows.Count; $r++) {
                if ($r -eq $existingIndex) { continue }
                $row = $dataRows[$r]
                $rowKey = if ($keyIdx -lt $row.Count) { $row[$keyIdx] } else { '' }
                $lines += Build-Row $cols $rowKey (Get-RowValues $cols $row) (Get-IdValue $cols $row)
            }
            $headerLine = ($cols | ForEach-Object { Quote-Field $_.Header }) -join ','
            $toWrite = (@($headerLine) + $lines -join $nl) + $nl
            [System.IO.File]::WriteAllText($fullPath, $toWrite, (New-Object System.Text.UTF8Encoding($hasBom)))
            $result.Success = $true
            $result.Message = "已从 $(Split-Path -Leaf $fullPath) 删除 Key「$OpKey」。"
            return $result
        }
        'Update' {
            if ($existingIndex -lt 0) { $result.Message = "找不到 Key「$OpKey」于 $(Split-Path -Leaf $fullPath)。"; return $result }
            if ($OpSet.Count -eq 0) { $result.Message = "Update 至少需要一个语言值。"; return $result }
            $lines = @()
            for ($r = 0; $r -lt $dataRows.Count; $r++) {
                $row = $dataRows[$r]
                $rowKey = if ($keyIdx -lt $row.Count) { $row[$keyIdx] } else { '' }
                $vals = Get-RowValues $cols $row
                if ($r -eq $existingIndex) { foreach ($k in $OpSet.Keys) { $vals[$k] = $OpSet[$k] } }
                $lines += Build-Row $cols $rowKey $vals (Get-IdValue $cols $row)
            }
            $headerLine = ($cols | ForEach-Object { Quote-Field $_.Header }) -join ','
            $toWrite = (@($headerLine) + $lines -join $nl) + $nl
            [System.IO.File]::WriteAllText($fullPath, $toWrite, (New-Object System.Text.UTF8Encoding($hasBom)))
            $result.Success = $true
            $result.Message = "已更新 $(Split-Path -Leaf $fullPath) 中 Key「$OpKey」的 $($OpSet.Count) 个语言列。"
            return $result
        }
        default {
            $result.Message = "未知 Action：$OpAction"
            return $result
        }
    }
}

# ===== Batch 模式 =====
if ($Action -eq 'Batch') {
    if ([string]::IsNullOrWhiteSpace($File)) { Write-Error "Batch 模式需要 -File 指定 JSON 文件路径。"; exit 1 }
    $jsonPath = Resolve-CsvPath $File
    if (-not (Test-Path -LiteralPath $jsonPath)) { Write-Error "找不到批处理文件：$jsonPath"; exit 1 }

    $jsonText = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8
    $ops = ConvertFrom-Json $jsonText
    if ($null -eq $ops) { Write-Error "JSON 解析为空或格式不对：$jsonPath"; exit 1 }

    $okCount = 0
    $failCount = 0
    foreach ($op in $ops) {
        $opSetDict = @{}
        if ($op.set) { $op.set.PSObject.Properties | ForEach-Object { $opSetDict[$_.Name] = "$($_.Value)" } }

        $r = Invoke-LocOp -OpAction $op.action -OpCsv $op.csv -OpKey $op.key -OpSet $opSetDict
        if ($r.Success) {
            $okCount++
            Write-Output "OK: $($r.Message)"
        }
        else {
            $failCount++
            Write-Output "FAIL [$($op.action) $($op.key)]: $($r.Message)"
        }
    }
    Write-Output "=== Batch 完成：成功 $okCount，失败 $failCount ==="
    if ($failCount -gt 0) { exit 1 } else { exit 0 }
}

# ===== 单条模式（Add/Remove/Update/Get） =====
if ([string]::IsNullOrWhiteSpace($Csv) -or [string]::IsNullOrWhiteSpace($Key)) {
    Write-Error "-Csv 和 -Key 为必填（Batch 模式除外）。"
    exit 1
}

# -Set "code=value" 解析为 dict
$setDict = @{}
foreach ($kv in $Set) {
    $idx = $kv.IndexOf('=')
    if ($idx -lt 0) { continue }
    $setDict[$kv.Substring(0, $idx)] = $kv.Substring($idx + 1)
}

$result = Invoke-LocOp -OpAction $Action -OpCsv $Csv -OpKey $Key -OpSet $setDict

if ($Action -eq 'Get') {
    if (-not $result.Success) { Write-Output $result.Message; exit 1 }
    $result.Message -split "`n" | ForEach-Object { Write-Output $_ }
    exit 0
}

if ($result.Success) {
    Write-Output "OK: $($result.Message)"
    exit 0
}
else {
    Write-Error $result.Message
    exit 1
}
