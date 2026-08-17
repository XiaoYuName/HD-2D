param(
    [string]$ProjectPath = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
try { $PSStyle.OutputRendering = "PlainText" } catch { }
try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }
try { [Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }

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

$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
$projectFile = Join-Path $PSScriptRoot "SourceCodeMcp.csproj"
$buildRoot = Join-Path $ProjectPath "Library\InspectorBridgeSourceCodeMcp"

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK 10 or newer is required but dotnet was not found."
}
$ripgrep = Get-Command rg -ErrorAction SilentlyContinue
if ($null -eq $ripgrep) {
    $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    $ripgrep = Get-ChildItem -Path (Join-Path $userProfile ".vscode\extensions\openai.chatgpt-*\bin\*\rg.exe") `
        -File -ErrorAction SilentlyContinue |
        Sort-Object -Property LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -ne $ripgrep) {
        $env:PATH = $ripgrep.DirectoryName + [IO.Path]::PathSeparator + $env:PATH
    }
}
if ($null -eq $ripgrep) {
    throw "ripgrep is required but rg was not found in PATH or the installed Codex extension."
}

$supportedSdk = dotnet --list-sdks | Where-Object {
    $_ -match '^\s*(\d+)\.' -and [int]$Matches[1] -ge 10
} | Select-Object -First 1
if ($null -eq $supportedSdk) {
    throw ".NET SDK 10 or newer is required."
}

$sourceIdentity = Get-ChildItem -LiteralPath $PSScriptRoot -File |
    Where-Object { $_.Extension -in @(".cs", ".csproj", ".props", ".json") } |
    Sort-Object -Property Name |
    ForEach-Object { "$($_.Name):$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)" }
$hashAlgorithm = [System.Security.Cryptography.SHA256]::Create()
try {
    $identityBytes = [System.Text.Encoding]::UTF8.GetBytes(($sourceIdentity -join "|"))
    $fingerprint = [Convert]::ToHexString($hashAlgorithm.ComputeHash($identityBytes)).ToLowerInvariant()
}
finally {
    $hashAlgorithm.Dispose()
}
$versionRoot = Join-Path $buildRoot ("versions\" + $fingerprint.Substring(0, 16))
$binRoot = Join-Path $versionRoot "bin"
$objRoot = Join-Path $versionRoot "obj"
$serverDll = Join-Path $binRoot "UnitySourceCodeMcp.dll"

if (-not (Test-Path -LiteralPath $serverDll)) {
    New-Item -ItemType Directory -Path $binRoot, $objRoot -Force | Out-Null
    $buildOutput = & dotnet build $projectFile `
        --nologo `
        --verbosity quiet `
        --output $binRoot `
        "-p:BaseIntermediateOutputPath=$objRoot\" `
        "-p:MSBuildProjectExtensionsPath=$objRoot\" 2>&1
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine(($buildOutput -join [Environment]::NewLine))
        throw "SourceCodeMcp build failed."
    }
}

& dotnet $serverDll --project-path $ProjectPath
exit $LASTEXITCODE
