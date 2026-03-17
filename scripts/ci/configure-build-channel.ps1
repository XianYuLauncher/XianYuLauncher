param(
    [Parameter(Mandatory = $true)]
    [string]$RefName,

    [string]$ManifestPath = "XianYuLauncher/Package.appxmanifest",

    [string]$GitHubEnvPath = $env:GITHUB_ENV
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

[xml]$xml = Get-Content -Path $ManifestPath

$version = $RefName
if ($version.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
    $version = $version.Substring(1)
}

if ($version -match '^(\d+\.\d+\.\d+)(.*)$') {
    $baseVersion = $matches[1]
    $suffix = $matches[2]
    $revision = 0

    if ($suffix -match '(\d+)$') {
        $revision = [int]$matches[1]
    }

    $cleanVersion = "$baseVersion.$revision"
    $xml.Package.Identity.Version = $cleanVersion
    Write-Host "Set Package Version: $cleanVersion (Base: $baseVersion, Rev: $revision)"
}

$isDevChannel = $RefName -match '-' -or -not ($RefName -match '^v\d')

if ($isDevChannel) {
    Write-Host 'Channel: DEV/BETA'

    $oldName = $xml.Package.Identity.Name
    $xml.Package.Identity.Name = "$oldName.Dev"
    Write-Host "Updated Identity Name: $oldName -> $($xml.Package.Identity.Name)"

    $xml.Package.Properties.DisplayName = 'XianYu Launcher (Dev)'

    if ($xml.Package.Applications.Application.VisualElements) {
        $xml.Package.Applications.Application.VisualElements.DisplayName = 'XianYu Launcher (Dev)'
    }

    $devAssetsPath = 'XianYuLauncher/Assets/Dev'
    $targetAssetsPath = 'XianYuLauncher/Assets'

    if (Test-Path $devAssetsPath) {
        Get-ChildItem -Path $devAssetsPath -File | ForEach-Object {
            $destination = Join-Path $targetAssetsPath $_.Name
            Copy-Item $_.FullName $destination -Force
            Write-Host "Replaced Asset: $($_.Name)"
        }
    }
    else {
        Write-Warning "Dev assets directory not found at $devAssetsPath"
    }

    $buildConstants = 'DEV_CHANNEL'
}
else {
    Write-Host 'Channel: STABLE'
    $buildConstants = 'RELEASE_CHANNEL'
}

$xml.Save($ManifestPath)

if (-not [string]::IsNullOrWhiteSpace($GitHubEnvPath)) {
    Add-Content -Path $GitHubEnvPath -Value "Build_Constants=$buildConstants"
}
else {
    Write-Host "Build_Constants=$buildConstants"
}