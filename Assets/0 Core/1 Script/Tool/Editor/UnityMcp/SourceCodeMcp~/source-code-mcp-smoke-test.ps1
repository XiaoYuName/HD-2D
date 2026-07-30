param(
    [string]$ProjectPath = "",
    [string]$ServerDll = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $cursor = Get-Item -LiteralPath $PSScriptRoot
    while ($null -ne $cursor -and
        (-not (Test-Path -LiteralPath (Join-Path $cursor.FullName "Assets")) -or
         -not (Test-Path -LiteralPath (Join-Path $cursor.FullName "ProjectSettings")))) {
        $cursor = $cursor.Parent
    }
    if ($null -eq $cursor) {
        throw "Cannot locate a Unity project above $PSScriptRoot. Pass -ProjectPath explicitly."
    }
    $ProjectPath = $cursor.FullName
}

$runRoot = Join-Path $ProjectPath ("Library\InspectorBridgeSourceCodeMcpTests\Run-" + [Guid]::NewGuid().ToString("N"))
$assetsRoot = Join-Path $runRoot "Assets\Scripts\VeryLongFeatureDirectoryNameForTokenDensity"
$stateRoot = Join-Path $runRoot "Library\UnityCompileMcp"
New-Item -ItemType Directory -Path $assetsRoot, $stateRoot, (Join-Path $runRoot "ProjectSettings") -Force | Out-Null

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "ASSERT: $Message" }
}

function Get-Sha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-SourceStamp {
    param([string]$AssetsPath)
    $files = @(Get-ChildItem -LiteralPath $AssetsPath -Recurse -Filter *.cs -File)
    $newest = [DateTime]::MinValue.Ticks
    [long]$length = 0
    foreach ($file in $files) {
        $newest = [Math]::Max($newest, $file.LastWriteTimeUtc.Ticks)
        $length += $file.Length
    }
    return "$newest|$($files.Count)|$length"
}

$alphaPath = Join-Path $assetsRoot "Worker.cs"
$betaPath = Join-Path $assetsRoot "WorkerConsumer.cs"
$crlfPath = Join-Path $assetsRoot "Crlf.txt"
$utf16Path = Join-Path $assetsRoot "Utf16.txt"
$binaryPath = Join-Path $assetsRoot "Binary.bin"
$calls = 1..20 | ForEach-Object { "        total += HitMe($_);" }
$alphaText = @(
    "namespace Demo;"
    "public class Worker"
    "{"
    "    public int HitMe(int value) => value + 1;"
    "    public int Run()"
    "    {"
    "        var total = 0;"
    $calls
    "        return total;"
    "    }"
    "}"
) -join "`n"
$betaText = @(
    "namespace Demo;"
    "public static class WorkerConsumer"
    "{"
    "    public static int Read() => new Worker().HitMe(2);"
    "}"
) -join "`n"
[System.IO.File]::WriteAllText($alphaPath, $alphaText + "`n", [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($betaPath, $betaText + "`n", [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText(
    $crlfPath,
    "First`r`nSecond`r`n",
    [System.Text.UTF8Encoding]::new($true))
[System.IO.File]::WriteAllText(
    $utf16Path,
    "First`nSecond`n",
    [System.Text.UnicodeEncoding]::new($false, $true, $true))
[System.IO.File]::WriteAllBytes($binaryPath, [byte[]](1, 0, 2, 3))

$projectXml = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Assets\Scripts\VeryLongFeatureDirectoryNameForTokenDensity\Worker.cs" />
    <Compile Include="Assets\Scripts\VeryLongFeatureDirectoryNameForTokenDensity\WorkerConsumer.cs" />
  </ItemGroup>
</Project>
'@
[System.IO.File]::WriteAllText((Join-Path $runRoot "Fixture.csproj"), $projectXml, [System.Text.UTF8Encoding]::new($false))
$solutionXml = @'
<Solution>
  <Project Path="Fixture.csproj" />
</Solution>
'@
[System.IO.File]::WriteAllText((Join-Path $runRoot "Fixture.slnx"), $solutionXml, [System.Text.UTF8Encoding]::new($false))

$state = [ordered]@{
    schemaVersion = 1
    reporterVersion = 4
    generation = 7
    unityPid = $PID
    isCompiling = $false
    hasResult = $true
    succeeded = $false
    errorCount = 1
    warningCount = 1
    startedAtUtc = [DateTime]::UtcNow.AddSeconds(-2).ToString("O")
    finishedAtUtc = [DateTime]::UtcNow.ToString("O")
    status = "failed"
    sourceStamp = Get-SourceStamp (Join-Path $runRoot "Assets")
    messages = @(
        [ordered]@{
            type = "error"
            message = "Fixture error"
            file = "Assets\Scripts\VeryLongFeatureDirectoryNameForTokenDensity\Worker.cs"
            line = 4
            column = 16
        },
        [ordered]@{
            type = "warning"
            message = "Fixture warning"
            file = "Assets\Scripts\VeryLongFeatureDirectoryNameForTokenDensity\WorkerConsumer.cs"
            line = 4
            column = 23
        }
    )
}
[System.IO.File]::WriteAllText(
    (Join-Path $stateRoot "compile-state-$PID.json"),
    ($state | ConvertTo-Json -Depth 8 -Compress),
    [System.Text.UTF8Encoding]::new($false))

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
if ([string]::IsNullOrWhiteSpace($ServerDll)) {
    $startInfo.FileName = "pwsh"
    $arguments = @(
        "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PSScriptRoot "source-code-mcp.ps1"), "-ProjectPath", $runRoot)
}
else {
    $startInfo.FileName = "dotnet"
    $arguments = @([System.IO.Path]::GetFullPath($ServerDll), "--project-path", $runRoot)
}
foreach ($argument in $arguments) {
    $startInfo.ArgumentList.Add($argument)
}
$server = [System.Diagnostics.Process]::new()
$server.StartInfo = $startInfo
[void]$server.Start()
$stderrTask = $server.StandardError.ReadToEndAsync()
$script:nextId = 0

function Send-Rpc {
    param([string]$Method, $Params = $null)
    $script:nextId++
    $request = [ordered]@{ jsonrpc = "2.0"; id = $script:nextId; method = $Method }
    if ($null -ne $Params) { $request.params = $Params }
    $server.StandardInput.WriteLine(($request | ConvertTo-Json -Depth 20 -Compress))
    $server.StandardInput.Flush()
    $line = $server.StandardOutput.ReadLine()
    if ([string]::IsNullOrWhiteSpace($line)) {
        throw "Server ended before responding. stderr: $($stderrTask.GetAwaiter().GetResult())"
    }
    try { return $line | ConvertFrom-Json -Depth 30 }
    catch { throw "Non-JSON stdout: $line" }
}

function Call-Tool {
    param([string]$Name, $Arguments)
    $rpc = Send-Rpc "tools/call" ([ordered]@{ name = $Name; arguments = $Arguments })
    Assert-True ($null -eq $rpc.error) "tools/call returned a JSON-RPC error for $Name"
    $payload = $rpc.result.content[0].text | ConvertFrom-Json -Depth 30
    return [pscustomobject]@{
        IsError = [bool]$rpc.result.isError
        Payload = $payload
        RawText = [string]$rpc.result.content[0].text
    }
}

try {
    $initialize = Send-Rpc "initialize" ([ordered]@{ protocolVersion = "2024-11-05"; capabilities = @{} })
    Assert-True ($initialize.result.serverInfo.name -eq "unity-source-code-mcp") "initialize serverInfo"
    $tools = Send-Rpc "tools/list"
    Assert-True (@($tools.result.tools).Count -eq 5) "tools/list should expose five tools"

    $search = Call-Tool "search_code" ([ordered]@{
        pattern = "HitMe"
        mode = "literal"
        paths = @("Assets")
        contextLines = 1
        maxResults = 50
        maxChars = 12000
    })
    Assert-True (-not $search.IsError) "search_code failed"
    Assert-True ($search.Payload.returnedMatches -eq 22) "search_code match count"
    Assert-True (@($search.Payload.files).Count -eq 2) "search_code grouping"
    $rawRg = (& rg -n -F "HitMe" (Join-Path $runRoot "Assets") | Out-String)
    Assert-True ($search.RawText.Length -le [int]($rawRg.Length * 0.60)) "grouped search payload should be <= 60% of rg output"
    $limitedSearch = Call-Tool "search_code" ([ordered]@{
        pattern = "HitMe"
        paths = @("Assets")
        maxResults = 5
    })
    Assert-True ($limitedSearch.Payload.returnedMatches -eq 5) "search result limit"
    Assert-True ($limitedSearch.Payload.truncated) "limited search should be truncated"
    $invalidRegex = Call-Tool "search_code" ([ordered]@{ pattern = "("; mode = "regex" })
    Assert-True ($invalidRegex.IsError) "invalid regex should fail"

    $alphaRelative = "Assets/Scripts/VeryLongFeatureDirectoryNameForTokenDensity/Worker.cs"
    $betaRelative = "Assets/Scripts/VeryLongFeatureDirectoryNameForTokenDensity/WorkerConsumer.cs"
    $alphaRead = Call-Tool "read_code" ([ordered]@{ path = $alphaRelative; startLine = 1; maxChars = 20000 })
    $betaRead = Call-Tool "read_code" ([ordered]@{ path = $betaRelative; startLine = 1; maxChars = 20000 })
    Assert-True (-not $alphaRead.Payload.truncated) "read_code unexpectedly truncated"
    Assert-True ($alphaRead.Payload.sha256 -eq (Get-Sha256 $alphaPath)) "read_code SHA-256"

    $alphaLines = @($alphaRead.Payload.content -split "`n")
    $returnLine = [Array]::IndexOf($alphaLines, "        return total;") + 1
    $createRelative = "Assets/Scripts/VeryLongFeatureDirectoryNameForTokenDensity/Created.txt"
    $patch = Call-Tool "apply_patch" ([ordered]@{
        files = @(
            [ordered]@{
                path = $alphaRelative
                expectedSha256 = $alphaRead.Payload.sha256
                edits = @([ordered]@{ startLine = $returnLine; deleteLineCount = 1; newText = "        return total + 1;" })
            },
            [ordered]@{
                path = $betaRelative
                expectedSha256 = $betaRead.Payload.sha256
                edits = @([ordered]@{ startLine = 4; deleteLineCount = 1; newText = "    public static int Read() => new Worker().HitMe(3);" })
            },
            [ordered]@{
                path = $createRelative
                create = $true
                edits = @([ordered]@{ startLine = 1; deleteLineCount = 0; newText = "Created by transaction`n" })
            }
        )
    })
    Assert-True (-not $patch.IsError) "apply_patch failed"
    Assert-True ($patch.Payload.fileCount -eq 3) "apply_patch file count"
    Assert-True ((Get-Content -LiteralPath $alphaPath -Raw).Contains("return total + 1;")) "alpha edit missing"
    Assert-True (Test-Path -LiteralPath (Join-Path $runRoot $createRelative)) "created file missing"

    $alphaAfter = Call-Tool "read_code" ([ordered]@{ path = $alphaRelative })
    $beforeFailedTransaction = [System.IO.File]::ReadAllBytes($alphaPath)
    $failedPatch = Call-Tool "apply_patch" ([ordered]@{
        files = @(
            [ordered]@{
                path = $alphaRelative
                expectedSha256 = $alphaAfter.Payload.sha256
                edits = @([ordered]@{ startLine = $returnLine; deleteLineCount = 1; newText = "        return total + 2;" })
            },
            [ordered]@{
                path = $betaRelative
                expectedSha256 = ("0" * 64)
                edits = @([ordered]@{ startLine = 4; deleteLineCount = 1; newText = "    public static int Read() => 0;" })
            }
        )
    })
    Assert-True ($failedPatch.IsError) "stale hash should fail"
    Assert-True (
        [System.Linq.Enumerable]::SequenceEqual(
            [byte[]]$beforeFailedTransaction,
            [byte[]][System.IO.File]::ReadAllBytes($alphaPath))) "failed transaction changed an earlier file"

    $crlfRelative = "Assets/Scripts/VeryLongFeatureDirectoryNameForTokenDensity/Crlf.txt"
    $crlfRead = Call-Tool "read_code" ([ordered]@{ path = $crlfRelative })
    $crlfPatch = Call-Tool "apply_patch" ([ordered]@{
        files = @([ordered]@{
            path = $crlfRelative
            expectedSha256 = $crlfRead.Payload.sha256
            edits = @([ordered]@{ startLine = 2; deleteLineCount = 1; newText = "Changed" })
        })
    })
    Assert-True (-not $crlfPatch.IsError) "CRLF patch failed"
    $crlfBytes = [System.IO.File]::ReadAllBytes($crlfPath)
    Assert-True ($crlfBytes[0] -eq 0xEF -and $crlfBytes[1] -eq 0xBB -and $crlfBytes[2] -eq 0xBF) "UTF-8 BOM was not preserved"
    Assert-True ([System.Text.Encoding]::UTF8.GetString($crlfBytes).Contains("`r`n")) "CRLF was not preserved"

    $utf16Relative = "Assets/Scripts/VeryLongFeatureDirectoryNameForTokenDensity/Utf16.txt"
    $utf16Read = Call-Tool "read_code" ([ordered]@{ path = $utf16Relative })
    Assert-True ($utf16Read.Payload.encoding -eq "utf-16-le") "UTF-16 detection"
    $utf16Patch = Call-Tool "apply_patch" ([ordered]@{
        files = @([ordered]@{
            path = $utf16Relative
            expectedSha256 = $utf16Read.Payload.sha256
            edits = @([ordered]@{ startLine = 2; deleteLineCount = 1; newText = "Changed" })
        })
    })
    Assert-True (-not $utf16Patch.IsError) "UTF-16 patch failed"
    $utf16Bytes = [System.IO.File]::ReadAllBytes($utf16Path)
    Assert-True ($utf16Bytes[0] -eq 0xFF -and $utf16Bytes[1] -eq 0xFE) "UTF-16 BOM was not preserved"

    $overlapBefore = [System.IO.File]::ReadAllBytes($alphaPath)
    $overlap = Call-Tool "apply_patch" ([ordered]@{
        files = @([ordered]@{
            path = $alphaRelative
            expectedSha256 = (Get-Sha256 $alphaPath)
            edits = @(
                [ordered]@{ startLine = 4; deleteLineCount = 1; newText = "    public int A;" },
                [ordered]@{ startLine = 4; deleteLineCount = 0; newText = "    public int B;" }
            )
        })
    })
    Assert-True ($overlap.IsError) "overlapping edits should fail"
    Assert-True (
        [System.Linq.Enumerable]::SequenceEqual(
            [byte[]]$overlapBefore,
            [byte[]][System.IO.File]::ReadAllBytes($alphaPath))) "overlapping edits changed the file"

    $symbol = Call-Tool "find_symbol" ([ordered]@{
        query = "Worker"
        kinds = @("class")
        includeReferences = $true
        maxResults = 50
        maxChars = 12000
    })
    Assert-True (-not $symbol.IsError) "find_symbol failed"
    Assert-True ($symbol.Payload.semanticMode -eq "semantic") "find_symbol did not use Roslyn semantic mode"
    Assert-True (@($symbol.Payload.symbols).Count -eq 1) "find_symbol declaration count"
    Assert-True (@($symbol.Payload.references).Count -ge 1) "find_symbol reference count"
    $symbolById = Call-Tool "find_symbol" ([ordered]@{
        symbolId = $symbol.Payload.symbols[0].symbolId
        includeReferences = $true
    })
    Assert-True (-not $symbolById.IsError) "find_symbol symbolId lookup failed"
    Assert-True (@($symbolById.Payload.references).Count -ge 1) "symbolId reference count"

    $untrackedPath = Join-Path $runRoot "Assets\Scripts\NewUntracked.cs"
    [System.IO.File]::WriteAllText(
        $untrackedPath,
        "namespace Demo; public class NewUntracked { }`n",
        [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::SetLastWriteTimeUtc(
        $untrackedPath,
        (Get-Item -LiteralPath (Join-Path $runRoot "Fixture.csproj")).LastWriteTimeUtc.AddSeconds(2))
    $fallback = Call-Tool "find_symbol" ([ordered]@{
        query = "NewUntracked"
        kinds = @("class")
    })
    Assert-True (-not $fallback.IsError) "find_symbol syntax fallback failed"
    Assert-True ($fallback.Payload.semanticMode -eq "syntax") "new untracked source should force syntax fallback"
    Assert-True (@($fallback.Payload.symbols).Count -eq 1) "syntax fallback declaration count"

    $diagnostics = Call-Tool "get_diagnostics" ([ordered]@{ severity = "error" })
    Assert-True (-not $diagnostics.IsError) "get_diagnostics failed"
    Assert-True ($diagnostics.Payload.available) "diagnostics unavailable"
    Assert-True ($diagnostics.Payload.stale) "diagnostics should be stale after source edit"
    Assert-True ($diagnostics.Payload.returnedResults -eq 1) "diagnostic severity filter"
    $warnings = Call-Tool "get_diagnostics" ([ordered]@{ severity = "warning" })
    Assert-True ($warnings.Payload.returnedResults -eq 1) "warning diagnostic filter"

    $binary = Call-Tool "read_code" ([ordered]@{
        path = "Assets/Scripts/VeryLongFeatureDirectoryNameForTokenDensity/Binary.bin"
    })
    Assert-True ($binary.IsError) "binary read should fail"
    $traversal = Call-Tool "read_code" ([ordered]@{ path = "../outside.txt" })
    Assert-True ($traversal.IsError) "path traversal should fail"

    Write-Output "UnitySourceCodeMcp smoke test passed."
}
finally {
    try { $server.StandardInput.Close() } catch { }
    if (-not $server.WaitForExit(10000)) {
        try { $server.Kill($true) } catch { }
    }
    $stderr = $stderrTask.GetAwaiter().GetResult()
    if ($server.ExitCode -ne 0 -and -not [string]::IsNullOrWhiteSpace($stderr)) {
        [Console]::Error.WriteLine($stderr)
    }
    $server.Dispose()
}
