# 多语言 CSV 行级增/删/改/查（不依赖 Unity 编辑器，纯文本操作，可在 Editor 打开工程时安全并行使用）。
# 解析/拼行规则与 Assets/0 Core/1 Script/Tool/Localization/Editor/LocCsvEditor.cs、LocCsvMerger.cs 保持一致：
#   - 表头沿用 Key,Id,语言列("语言名(代码)")；Key/非空语言列加引号，Id 与空语言列留空不加引号。
#   - 引号转义：内部 " 变 ""；保留原文件 BOM 与换行风格。
# 单条用法（务必用 & 调用操作符直接调脚本，同一进程内解析参数数组；
#      不要套一层 `powershell -File ...`，多进程转发命令行会打乱带引号/逗号的值）：
#   （-Csv 传相对路径时以仓库根目录——含 Assets/ 或 .git 的目录——为基准，而非脚本自身所在目录）
#   & Tools/LocCsv.ps1 -Action Add    -Csv <相对/绝对路径> -Key <Key> -Set 'code=值','code=值',...
#   & Tools/LocCsv.ps1 -Action Update -Csv <路径> -Key <Key> -Set 'code=值',...      # 只改指定语言列，其余列保持原值
#   & Tools/LocCsv.ps1 -Action Remove -Csv <路径> -Key <Key>
#   & Tools/LocCsv.ps1 -Action Get    -Csv <路径> -Key <Key>                        # 只打印该 Key 各语言值，不用整份读文件
#   & Tools/LocCsv.ps1 -Action Import -Csv <路径> [-Rebuild]                       # 请求 Unity 导入；-Rebuild 会先清空目标表
#   & Tools/LocCsv.ps1 -Action NormalizeKeys -Csv <路径>                             # 清理全部 Key 首尾空白并保留 Id/文案
# -Set 多个语言必须作为一个数组传（同一个 -Set 后跟逗号分隔的多个值），不能重复写多个 -Set。
#
# 批量用法（一次要加/删多条时用这个，别手写循环反复起进程调用）：
#   & Tools/LocCsv.ps1 -Action Batch -File <JSON 文件路径>
#   & Tools/LocCsv.ps1 -Action Batch -Json '<JSON 数组>' -Quiet -Import
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
# 默认只改 CSV 源文件；追加 -Import 可请求已打开（或下次打开）的 Unity 编辑器按工作台映射增量导入。
# 删除/重命名 Key 后，用 -Action Import -Csv <路径> -Rebuild 让 StringTable 与工作台关联的 CSV 完全同步。

param(
    [Parameter(Mandatory = $true)][ValidateSet('Add', 'Remove', 'Update', 'Get', 'Import', 'Batch', 'RemoveComments', 'NormalizeKeys')][string]$Action,
    [string]$Csv,
    [string]$Key,
    [string[]]$Set = @(),
    [string]$File,   # -Action Batch：JSON 数组文件路径（与 -Json 二选一）
    [string]$Json,   # -Action Batch：直接传 JSON 数组，免临时文件
    [switch]$Quiet,  # 成功时只保留 Batch 汇总；失败与警告始终输出
    [switch]$AutoFill, # 按源语言文本从项目其它 *Loc.csv 复用已有译文
    [string]$SourceLocale = 'zh-CN',
    [ValidateSet('Warn', 'Error', 'Ignore')][string]$DuplicatePolicy = 'Warn',
    [switch]$Import, # 成功写入后请求已打开的 Unity 编辑器增量导入对应表
    [switch]$Rebuild # 仅用于 -Action Import：先清空目标表，再导入工作台关联的全部 CSV
)

$ErrorActionPreference = 'Stop'

# 向上查找仓库根目录（含 Assets 或 .git 的目录），相对路径一律以仓库根为基准，
# 而不是以本脚本所在目录为基准——避免调用方按"仓库根相对路径"传参时被解析到错误位置。
function Find-RepoRoot([string]$startDir) {
    $dir = $startDir
    while ($dir) {
        if ((Test-Path (Join-Path $dir 'Assets')) -or (Test-Path (Join-Path $dir '.git'))) { return $dir }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    return $startDir
}
$RepoRoot = Find-RepoRoot $PSScriptRoot
$TranslationMemoryCache = @{}

function Resolve-CsvPath([string]$path) {
    if ([System.IO.Path]::IsPathRooted($path)) { return $path }
    $resolved = Join-Path $RepoRoot $path
    if (-not (Test-Path $resolved)) {
        Write-Error "找不到文件：$resolved（相对路径基准目录：$RepoRoot）"
    }
    return $resolved
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

function Get-ObjectProperty($obj, [string]$name, $defaultValue) {
    if ($null -eq $obj) { return $defaultValue }
    $prop = $obj.PSObject.Properties[$name]
    if ($null -eq $prop -or $null -eq $prop.Value) { return $defaultValue }
    return $prop.Value
}

# 翻译记忆：在项目全部 *Loc.csv 中按源语言精确匹配，复用该行其它语言。
# 仅填充调用方未提供的语言；不会覆盖显式传入值。
function Fill-FromTranslationMemory($cols, [hashtable]$values, [string]$sourceCode) {
    $warnings = New-Object System.Collections.Generic.List[string]
    if (-not $values.ContainsKey($sourceCode) -or [string]::IsNullOrWhiteSpace($values[$sourceCode])) {
        [void]$warnings.Add("AutoFill 需要非空源语言「$sourceCode」。")
        return [PSCustomObject]@{ Values = $values; Warnings = $warnings; Filled = 0 }
    }

    $wanted = @($cols | Where-Object { $_.Code -and (-not $values.ContainsKey($_.Code) -or [string]::IsNullOrEmpty($values[$_.Code])) } | ForEach-Object { $_.Code })
    if ($wanted.Count -eq 0) { return [PSCustomObject]@{ Values = $values; Warnings = $warnings; Filled = 0 } }

    $sourceText = $values[$sourceCode]
    $filled = 0
    if (-not $TranslationMemoryCache.ContainsKey($sourceCode)) {
        $index = @{}
        $files = Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'Assets') -Recurse -Filter '*Loc.csv' -File
        foreach ($file in $files) {
            $memoryRows = Parse-Csv ([System.IO.File]::ReadAllText($file.FullName))
            if ($memoryRows.Count -lt 2) { continue }
            $memoryCols = Get-Columns $memoryRows[0]
            $sourceCol = $memoryCols | Where-Object { $_.Code -eq $sourceCode } | Select-Object -First 1
            if ($null -eq $sourceCol) { continue }
            for ($i = 1; $i -lt $memoryRows.Count; $i++) {
                $memoryRow = $memoryRows[$i]
                if ($sourceCol.Index -ge $memoryRow.Count -or [string]::IsNullOrEmpty($memoryRow[$sourceCol.Index])) { continue }
                $text = $memoryRow[$sourceCol.Index]
                if (-not $index.ContainsKey($text)) { $index[$text] = Get-RowValues $memoryCols $memoryRow }
            }
        }
        $TranslationMemoryCache[$sourceCode] = $index
    }
    $memory = $TranslationMemoryCache[$sourceCode]
    if ($memory.ContainsKey($sourceText)) {
        $memoryValues = $memory[$sourceText]
        foreach ($code in @($wanted)) {
            if ($memoryValues.ContainsKey($code) -and -not [string]::IsNullOrEmpty($memoryValues[$code])) {
                $values[$code] = $memoryValues[$code]
                $wanted = @($wanted | Where-Object { $_ -ne $code })
                $filled++
            }
        }
    }
    if ($wanted.Count -gt 0) {
        [void]$warnings.Add("AutoFill 未找到以下语言的翻译记忆：$($wanted -join ', ')。")
    }
    return [PSCustomObject]@{ Values = $values; Warnings = $warnings; Filled = $filled }
}

function Find-DuplicateValues($cols, $dataRows, [hashtable]$values, [string]$currentKey) {
    $matches = New-Object System.Collections.Generic.List[string]
    foreach ($row in $dataRows) {
        $rowValues = Get-RowValues $cols $row
        $keyCol = $cols | Where-Object { $_.IsKey } | Select-Object -First 1
        $rowKey = if ($keyCol.Index -lt $row.Count) { $row[$keyCol.Index] } else { '' }
        if ($rowKey -eq $currentKey) { continue }
        foreach ($code in $values.Keys) {
            if ([string]::IsNullOrEmpty($values[$code])) { continue }
            if ($rowValues.ContainsKey($code) -and $rowValues[$code] -eq $values[$code]) {
                [void]$matches.Add("$code 与「$rowKey」相同")
            }
        }
    }
    return @($matches | Select-Object -Unique)
}

function Request-UnityImport([string[]]$csvPaths, [bool]$clearFirst = $false) {
    $unique = @($csvPaths | Where-Object { $_ } | ForEach-Object { (Resolve-CsvPath $_) } | Select-Object -Unique)
    if ($unique.Count -eq 0) { return }
    $requestPath = Join-Path $RepoRoot 'Library/LocCsvImportRequest.json'
    $payload = @{
        csvPaths = $unique
        clearFirst = $clearFirst
        requestedAt = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($requestPath, $payload, (New-Object System.Text.UTF8Encoding($false)))
}

# 单条操作的核心实现：不 exit/Write-Error，只回填 $result，方便单条调用与 Batch 循环共用。
function Invoke-LocOp {
    param(
        [Parameter(Mandatory = $true)][string]$OpAction,
        [Parameter(Mandatory = $true)][string]$OpCsv,
        [Parameter(Mandatory = $true)][string]$OpKey,
        [hashtable]$OpSet = @{},
        [bool]$OpAutoFill = $false,
        [string]$OpSourceLocale = 'zh-CN',
        [ValidateSet('Warn', 'Error', 'Ignore')][string]$OpDuplicatePolicy = 'Warn'
    )

    $result = [PSCustomObject]@{ Success = $false; Changed = $false; Message = ''; Values = $null; Warnings = @() }

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
        'RemoveComments' {
            $keepCols = @($cols | Where-Object { $_.Header -notmatch 'Comments' })
            if ($keepCols.Count -eq $cols.Count) {
                $result.Message = "未找到 Comments 列：$(Split-Path -Leaf $fullPath)"; return $result
            }
            $lines = @()
            $headerLine = ($keepCols | ForEach-Object { Quote-Field $_.Header }) -join ','
            $lines += $headerLine
            foreach ($row in $dataRows) {
                $cells = foreach ($col in $keepCols) {
                    if ($col.Index -lt $row.Count) {
                        $value = $row[$col.Index]
                        if ($col.IsKey -or $col.IsId -or -not [string]::IsNullOrEmpty($value)) { Quote-Field $value } else { '' }
                    } else { '' }
                }
                $lines += ($cells -join ',')
            }
            [System.IO.File]::WriteAllText($fullPath, (($lines -join $nl) + $nl), (New-Object System.Text.UTF8Encoding($hasBom)))
            $result.Success = $true
            $result.Changed = $true
            $result.Message = "已从 $(Split-Path -Leaf $fullPath) 删除 $($cols.Count - $keepCols.Count) 个 Comments 列。"
            return $result
        }
        'Add' {
            if ($existingIndex -ge 0) { $result.Message = "Key「$OpKey」已存在于 $(Split-Path -Leaf $fullPath)，未添加。"; return $result }
            if ($OpAutoFill) {
                $fill = Fill-FromTranslationMemory $cols $OpSet $OpSourceLocale
                $OpSet = $fill.Values
                $result.Warnings += @($fill.Warnings)
            }
            if ($OpDuplicatePolicy -ne 'Ignore') {
                $duplicates = Find-DuplicateValues $cols $dataRows $OpSet $OpKey
                if ($duplicates.Count -gt 0) {
                    $dupMessage = "检测到重复文案：$($duplicates -join '；')。"
                    if ($OpDuplicatePolicy -eq 'Error') { $result.Message = $dupMessage; return $result }
                    $result.Warnings += $dupMessage
                }
            }
            $newLine = Build-Row $cols $OpKey $OpSet $null
            $toWrite = $content
            if ($toWrite.Length -gt 0 -and -not $toWrite.EndsWith("`n")) { $toWrite += $nl }
            $toWrite += $newLine + $nl
            [System.IO.File]::WriteAllText($fullPath, $toWrite, (New-Object System.Text.UTF8Encoding($hasBom)))
            $result.Success = $true
            $result.Changed = $true
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
            $result.Changed = $true
            $result.Message = "已从 $(Split-Path -Leaf $fullPath) 删除 Key「$OpKey」。"
            return $result
        }
        'Update' {
            if ($existingIndex -lt 0) { $result.Message = "找不到 Key「$OpKey」于 $(Split-Path -Leaf $fullPath)。"; return $result }
            if ($OpSet.Count -eq 0) { $result.Message = "Update 至少需要一个语言值。"; return $result }
            if ($OpAutoFill) {
                $fill = Fill-FromTranslationMemory $cols $OpSet $OpSourceLocale
                $OpSet = $fill.Values
                $result.Warnings += @($fill.Warnings)
            }
            if ($OpDuplicatePolicy -ne 'Ignore') {
                $duplicates = Find-DuplicateValues $cols $dataRows $OpSet $OpKey
                if ($duplicates.Count -gt 0) {
                    $dupMessage = "检测到重复文案：$($duplicates -join '；')。"
                    if ($OpDuplicatePolicy -eq 'Error') { $result.Message = $dupMessage; return $result }
                    $result.Warnings += $dupMessage
                }
            }
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
            $result.Changed = $true
            $result.Message = "已更新 $(Split-Path -Leaf $fullPath) 中 Key「$OpKey」的 $($OpSet.Count) 个语言列。"
            return $result
        }
        default {
            $result.Message = "未知 Action：$OpAction"
            return $result
        }
    }
}

# ===== 显式导入（无需修改 CSV） =====
if ($Action -eq 'Import') {
    if ([string]::IsNullOrWhiteSpace($Csv)) { Write-Error "Import 需要 -Csv <路径>。"; exit 1 }
    Request-UnityImport @($Csv) $Rebuild.IsPresent
    if (-not $Quiet) {
        $mode = if ($Rebuild) { '重建' } else { '增量' }
        Write-Output "OK: 已请求 Unity $mode 导入该 CSV 对应的字符串表。"
    }
    exit 0
}

# ===== Key 规范化 =====
if ($Action -eq 'NormalizeKeys') {
    if ([string]::IsNullOrWhiteSpace($Csv)) { Write-Error "NormalizeKeys 需要 -Csv <路径>。"; exit 1 }

    $fullPath = Resolve-CsvPath $Csv
    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $content = [System.IO.File]::ReadAllText($fullPath)
    $nl = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
    $rows = Parse-Csv $content
    if ($rows.Count -lt 1) { Write-Error "CSV 解析失败或为空：$fullPath"; exit 1 }

    $cols = Get-Columns $rows[0]
    $keyCol = $cols | Where-Object { $_.IsKey } | Select-Object -First 1
    if ($null -eq $keyCol) { Write-Error "CSV 表头缺少 Key 列：$fullPath"; exit 1 }

    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    $lines = @()
    $changedCount = 0
    for ($r = 1; $r -lt $rows.Count; $r++) {
        $row = $rows[$r]
        $key = if ($keyCol.Index -lt $row.Count) { $row[$keyCol.Index].Trim() } else { '' }
        if ([string]::IsNullOrEmpty($key)) { Write-Error "第 $r 行 Key 规范化后为空，已中止。"; exit 1 }
        if (-not $seen.Add($key)) { Write-Error "Key「$key」规范化后重复，已中止。"; exit 1 }
        if ($keyCol.Index -lt $row.Count -and $row[$keyCol.Index] -cne $key) { $changedCount++ }
        $lines += Build-Row $cols $key (Get-RowValues $cols $row) (Get-IdValue $cols $row)
    }

    if ($changedCount -gt 0) {
        $headerLine = ($cols | ForEach-Object { Quote-Field $_.Header }) -join ','
        [System.IO.File]::WriteAllText($fullPath, ((@($headerLine) + $lines -join $nl) + $nl),
            (New-Object System.Text.UTF8Encoding($hasBom)))
        if ($Import) { Request-UnityImport @($Csv) }
    }
    if (-not $Quiet) { Write-Output "OK: 已规范化 $(Split-Path -Leaf $fullPath) 的 $changedCount 个 Key，Id 与文案保持不变。" }
    exit 0
}

# ===== Batch 模式 =====
if ($Action -eq 'Batch') {
    if (-not [string]::IsNullOrWhiteSpace($File) -and -not [string]::IsNullOrWhiteSpace($Json)) {
        Write-Error "Batch 模式的 -File 与 -Json 只能使用一个。"; exit 1
    }
    if (-not [string]::IsNullOrWhiteSpace($Json)) {
        $jsonText = $Json
        $jsonSource = '内联 JSON'
    }
    elseif (-not [string]::IsNullOrWhiteSpace($File)) {
        $jsonPath = Resolve-CsvPath $File
        if (-not (Test-Path -LiteralPath $jsonPath)) { Write-Error "找不到批处理文件：$jsonPath"; exit 1 }
        $jsonText = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8
        $jsonSource = $jsonPath
    }
    else {
        Write-Error "Batch 模式需要 -File <路径> 或 -Json '<JSON 数组>'。"; exit 1
    }
    $ops = ConvertFrom-Json $jsonText
    if ($null -eq $ops) { Write-Error "JSON 解析为空或格式不对：$jsonSource"; exit 1 }

    $okCount = 0
    $failCount = 0
    $warningCount = 0
    $changedCsvs = New-Object System.Collections.Generic.List[string]
    foreach ($op in $ops) {
        $opSetDict = @{}
        if ($op.set) { $op.set.PSObject.Properties | ForEach-Object { $opSetDict[$_.Name] = "$($_.Value)" } }

        $opAutoFill = [bool](Get-ObjectProperty $op 'autoFill' $AutoFill.IsPresent)
        $opSourceLocale = [string](Get-ObjectProperty $op 'sourceLocale' $SourceLocale)
        $opDuplicatePolicy = [string](Get-ObjectProperty $op 'duplicatePolicy' $DuplicatePolicy)
        $r = Invoke-LocOp -OpAction $op.action -OpCsv $op.csv -OpKey $op.key -OpSet $opSetDict `
            -OpAutoFill $opAutoFill -OpSourceLocale $opSourceLocale -OpDuplicatePolicy $opDuplicatePolicy
        if ($r.Success) {
            $okCount++
            if ($r.Changed) { [void]$changedCsvs.Add([string]$op.csv) }
            if (-not $Quiet) { Write-Output "OK: $($r.Message)" }
        }
        else {
            $failCount++
            Write-Output "FAIL [$($op.action) $($op.key)]: $($r.Message)"
        }
        foreach ($warning in @($r.Warnings)) {
            if ([string]::IsNullOrWhiteSpace($warning)) { continue }
            $warningCount++
            Write-Output "WARN [$($op.action) $($op.key)]: $warning"
        }
    }
    if ($Import -and $changedCsvs.Count -gt 0) {
        Request-UnityImport @($changedCsvs)
        if (-not $Quiet) { Write-Output "OK: 已请求 Unity 增量导入 $(@($changedCsvs | Select-Object -Unique).Count) 份 CSV。" }
    }
    Write-Output "=== Batch 完成：成功 $okCount，失败 $failCount，警告 $warningCount ==="
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

$result = Invoke-LocOp -OpAction $Action -OpCsv $Csv -OpKey $Key -OpSet $setDict `
    -OpAutoFill $AutoFill.IsPresent -OpSourceLocale $SourceLocale -OpDuplicatePolicy $DuplicatePolicy

if ($Action -eq 'Get') {
    if (-not $result.Success) { Write-Output $result.Message; exit 1 }
    $result.Message -split "`n" | ForEach-Object { Write-Output $_ }
    exit 0
}

if ($result.Success) {
    foreach ($warning in @($result.Warnings)) {
        if (-not [string]::IsNullOrWhiteSpace($warning)) { Write-Output "WARN: $warning" }
    }
    if ($Import -and $result.Changed) {
        Request-UnityImport @($Csv)
        if (-not $Quiet) { Write-Output "OK: 已请求 Unity 增量导入该 CSV。" }
    }
    if (-not $Quiet) { Write-Output "OK: $($result.Message)" }
    exit 0
}
else {
    Write-Error $result.Message
    exit 1
}
