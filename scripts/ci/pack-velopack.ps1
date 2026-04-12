param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory,

    [Parameter(Mandatory = $true)]
    [string]$PackVersion,

    [Parameter(Mandatory = $true)]
    [string]$Channel,

    [Parameter(Mandatory = $true)]
    [string]$RuntimeIdentifier,

    [string]$VpkExecutablePath = 'vpk',

    [string]$PackId = 'SpiritStudio.XianYuLauncher',

    [string]$PackTitle = '',

    [string]$PackAuthors = 'Spirit Studio',

    [string]$MainExecutableName = 'XianYuLauncher.exe',

    [string]$IconPath = 'XianYuLauncher/Assets/WindowIcon.ico',

    [string]$FrameworkRuntime
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Resolve-CommandPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandName
    )

    if ($CommandName.IndexOfAny([char[]]@('\', '/')) -ge 0 -or $CommandName.EndsWith('.exe', [System.StringComparison]::OrdinalIgnoreCase)) {
        $resolvedPath = Resolve-AbsolutePath -Path $CommandName
        if (-not (Test-Path $resolvedPath -PathType Leaf)) {
            throw "Velopack executable not found: $resolvedPath"
        }

        return $resolvedPath
    }

    $command = Get-Command $CommandName -ErrorAction Stop
    return $command.Source
}

function Resolve-FrameworkRuntime {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Rid
    )

    switch ($Rid.ToLowerInvariant()) {
        'win-x86' { return 'net10-x86-desktop' }
        'win-x64' { return 'net10-x64-desktop' }
        'win-arm64' { return 'net10-arm64-desktop' }
        default { return $null }
    }
}

$publishDirectoryPath = Resolve-AbsolutePath -Path $PublishDirectory
if (-not (Test-Path $publishDirectoryPath -PathType Container)) {
    throw "Publish directory not found: $publishDirectoryPath"
}

$mainExecutablePath = Join-Path $publishDirectoryPath $MainExecutableName
if (-not (Test-Path $mainExecutablePath -PathType Leaf)) {
    throw "Main executable missing from publish directory: $mainExecutablePath"
}

$iconFilePath = Resolve-AbsolutePath -Path $IconPath
if (-not (Test-Path $iconFilePath -PathType Leaf)) {
    throw "Velopack pack icon not found: $iconFilePath"
}

$releaseDirectoryPath = Resolve-AbsolutePath -Path $ReleaseDirectory
New-Item -ItemType Directory -Force -Path $releaseDirectoryPath | Out-Null

$vpkPath = Resolve-CommandPath -CommandName $VpkExecutablePath
$resolvedPackTitle = if ([string]::IsNullOrWhiteSpace($PackTitle)) {
    if ($Channel.Equals('stable', [System.StringComparison]::OrdinalIgnoreCase)) {
        'XianYu Launcher'
    }
    else {
        'XianYu Launcher (Dev)'
    }
}
else {
    $PackTitle
}

$resolvedFrameworkRuntime = if ([string]::IsNullOrWhiteSpace($FrameworkRuntime)) {
    Resolve-FrameworkRuntime -Rid $RuntimeIdentifier
}
else {
    $FrameworkRuntime
}

$arguments = [System.Collections.Generic.List[string]]::new()
$arguments.Add('pack')
$arguments.Add('--packId')
$arguments.Add($PackId)
$arguments.Add('--packVersion')
$arguments.Add($PackVersion)
$arguments.Add('--packDir')
$arguments.Add($publishDirectoryPath)
$arguments.Add('--mainExe')
$arguments.Add($MainExecutableName)
$arguments.Add('--outputDir')
$arguments.Add($releaseDirectoryPath)
$arguments.Add('--channel')
$arguments.Add($Channel)
$arguments.Add('--runtime')
$arguments.Add($RuntimeIdentifier)
$arguments.Add('--packTitle')
$arguments.Add($resolvedPackTitle)
$arguments.Add('--packAuthors')
$arguments.Add($PackAuthors)
$arguments.Add('--icon')
$arguments.Add($iconFilePath)
$arguments.Add('--noPortable')

if (-not [string]::IsNullOrWhiteSpace($resolvedFrameworkRuntime)) {
    $arguments.Add('--framework')
    $arguments.Add($resolvedFrameworkRuntime)
}

Write-Host "Packing Velopack release with channel '$Channel' and version '$PackVersion'."
Write-Host "Using pack title: $resolvedPackTitle"
Write-Host "Using vpk: $vpkPath"
Write-Host "Publish directory: $publishDirectoryPath"
Write-Host "Release directory: $releaseDirectoryPath"

& $vpkPath $arguments

if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code: $LASTEXITCODE"
}

$releaseMetadataPath = Join-Path $releaseDirectoryPath "releases.$Channel.json"
if (-not (Test-Path $releaseMetadataPath -PathType Leaf)) {
    throw "Velopack pack output missing release metadata: $releaseMetadataPath"
}

$setupExecutables = @(Get-ChildItem -Path $releaseDirectoryPath -Filter '*Setup.exe' -File)
if ($setupExecutables.Count -eq 0) {
    throw "Velopack pack output missing Setup.exe: $releaseDirectoryPath"
}

$packageFiles = @(Get-ChildItem -Path $releaseDirectoryPath -Filter '*.nupkg' -File)
if ($packageFiles.Count -eq 0) {
    throw "Velopack pack output missing nupkg package: $releaseDirectoryPath"
}

$legacyPackageFiles = @($packageFiles | Where-Object { $_.Name -notlike "*$PackVersion*" })
foreach ($legacyPackageFile in $legacyPackageFiles) {
    Remove-Item -Path $legacyPackageFile.FullName -Force
}

$packageFiles = @(Get-ChildItem -Path $releaseDirectoryPath -Filter '*.nupkg' -File)

$deltaPackageCount = @($packageFiles | Where-Object { $_.Name -match 'delta' }).Count
Write-Host "Generated $($packageFiles.Count) nupkg package(s), including $deltaPackageCount delta package(s)."
Get-ChildItem -Path $releaseDirectoryPath -File | Sort-Object Name | ForEach-Object {
    Write-Host "Generated asset: $($_.Name)"
}