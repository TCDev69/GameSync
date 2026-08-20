#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes GameSync (unpackaged, self-contained win-x64) and builds the Inno Setup installer.

.PARAMETER Version
  Semantic version (MAJOR.MINOR.PATCH). Defaults to Directory.Build.props Version, else 1.0.0.

.PARAMETER SkipPublish
  Only compile the Inno script (expects artifacts/publish already populated).

.PARAMETER InnoCompiler
  Path to ISCC.exe. Auto-detected from Program Files when omitted.
#>
[CmdletBinding()]
param(
    [string]$Version = "",
    [switch]$SkipPublish,
    [string]$InnoCompiler = ""
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

function Get-DefaultVersion {
    $props = Join-Path $Root "Directory.Build.props"
    if (Test-Path $props) {
        $match = Select-String -Path $props -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
        if ($match) { return $match.Matches[0].Groups[1].Value.Trim() }
    }
    return "1.0.0"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-DefaultVersion
}

$PublishDir = Join-Path $Root "artifacts\publish"
$ArtifactsDir = Join-Path $Root "artifacts"
$Iss = Join-Path $Root "installer\GameSync.iss"

if (-not $SkipPublish) {
    Write-Host "Publishing GameSync $Version (unpackaged win-x64)..."
    if (Test-Path $PublishDir) {
        Remove-Item -Recurse -Force $PublishDir
    }
    New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

    dotnet publish (Join-Path $Root "src\GameSync.App\GameSync.App.csproj") `
        -c Release `
        -p:Platform=x64 `
        -p:RuntimeIdentifier=win-x64 `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:SelfContained=true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:Version=$Version `
        -o $PublishDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    $exe = Join-Path $PublishDir "GameSync.exe"
    if (-not (Test-Path $exe)) {
        throw "Expected GameSync.exe at $exe after publish."
    }
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish output missing: $PublishDir"
}

function Find-Iscc {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path $Explicit)) { return (Resolve-Path $Explicit).Path }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }

    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$iscc = Find-Iscc -Explicit $InnoCompiler
if (-not $iscc) {
    throw "Inno Setup 6 compiler (ISCC.exe) not found. Install from https://jrsoftware.org/isinfo.php"
}

New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null
Write-Host "Compiling Inno Setup installer (version $Version)..."
& $iscc "/DMyAppVersion=$Version" $Iss
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE"
}

$setup = Join-Path $ArtifactsDir "GameSync-Setup-x64.exe"
if (-not (Test-Path $setup)) {
    throw "Expected installer at $setup"
}

Write-Host "Done: $setup"
Get-Item $setup | Format-List FullName, Length, LastWriteTime
