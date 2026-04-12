param(
    [Parameter(Mandatory = $true)]
    [string]$RefName,

    [string]$ManifestPath = "XianYuLauncher/Package.appxmanifest",

    [switch]$UpdateManifest,

    [string]$GitHubEnvPath = $env:GITHUB_ENV,

    [string]$GitHubOutputPath = $env:GITHUB_OUTPUT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-GitHubKeyValue {
    param(
        [string]$Path,
        [string]$Name,
        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $resolvedValue = if ($null -eq $Value) { '' } else { $Value }
    Add-Content -Path $Path -Value "$Name=$resolvedValue"
}

function Add-DevSuffix {
    param(
        [AllowNull()]
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Suffix
    )

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.EndsWith($Suffix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $Value
    }

    return "$Value$Suffix"
}

function Remove-DevSuffix {
    param(
        [AllowNull()]
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Suffix
    )

    if ([string]::IsNullOrWhiteSpace($Value) -or -not $Value.EndsWith($Suffix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $Value
    }

    return $Value.Substring(0, $Value.Length - $Suffix.Length)
}

function Get-ChannelMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRefName
    )

    $rawVersion = $SourceRefName
    if ($rawVersion.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
        $rawVersion = $rawVersion.Substring(1)
    }

    $displayVersion = if ($SourceRefName.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
        $SourceRefName
    }
    else {
        $rawVersion
    }

    $packageVersion = $null
    $artifactVersion = $rawVersion

    if ($rawVersion -match '^(\d+\.\d+\.\d+)(.*)$') {
        $baseVersion = $matches[1]
        $suffix = $matches[2]
        $revision = 0

        if ($suffix -match '(\d+)$') {
            $revision = [int]$matches[1]
        }

        $packageVersion = "$baseVersion.$revision"
        $artifactVersion = $packageVersion
    }
    else {
        $artifactVersion = ($rawVersion -replace '[^0-9A-Za-z\.-]', '-')
    }

    $channel = if ($SourceRefName -match 'dev') {
        'dev'
    }
    elseif ($SourceRefName -match 'beta') {
        'beta'
    }
    elseif ($SourceRefName -match '-') {
        'preview'
    }
    else {
        'stable'
    }

    $buildConstants = if ($channel -eq 'stable') {
        'RELEASE_CHANNEL'
    }
    else {
        'DEV_CHANNEL'
    }

    [pscustomobject]@{
        Channel = $channel
        BuildConstants = $buildConstants
        DisplayVersion = $displayVersion
        PackageVersion = $packageVersion
        ArtifactVersion = $artifactVersion
        UseDevBranding = $channel -ne 'stable'
    }
}

function Apply-DevAssets {
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
}

function Update-ChannelManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Metadata
    )

    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load($Path)

    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $namespaceManager.AddNamespace('appx', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $namespaceManager.AddNamespace('uap', 'http://schemas.microsoft.com/appx/manifest/uap/windows10')

    $identityNode = $xml.SelectSingleNode('/appx:Package/appx:Identity', $namespaceManager)
    $displayNameNode = $xml.SelectSingleNode('/appx:Package/appx:Properties/appx:DisplayName', $namespaceManager)
    $visualElementsNode = $xml.SelectSingleNode('/appx:Package/appx:Applications/appx:Application/uap:VisualElements', $namespaceManager)

    if ($null -eq $identityNode) {
        throw "Failed to locate Package/Identity in manifest: $Path"
    }

    if (-not [string]::IsNullOrWhiteSpace($Metadata.PackageVersion)) {
        $identityNode.SetAttribute('Version', $Metadata.PackageVersion)
        Write-Host "Set Package Version: $($Metadata.PackageVersion)"
    }

    $identityName = $identityNode.GetAttribute('Name')
    $packageDisplayName = if ($null -ne $displayNameNode) { $displayNameNode.InnerText } else { $null }
    $visualDisplayName = if ($null -ne $visualElementsNode) { $visualElementsNode.GetAttribute('DisplayName') } else { $null }

    if ($Metadata.UseDevBranding) {
        $updatedIdentityName = Add-DevSuffix -Value $identityName -Suffix '.Dev'
        $updatedPackageDisplayName = Add-DevSuffix -Value $packageDisplayName -Suffix ' (Dev)'
        $updatedVisualDisplayName = Add-DevSuffix -Value $visualDisplayName -Suffix ' (Dev)'
    }
    else {
        $updatedIdentityName = Remove-DevSuffix -Value $identityName -Suffix '.Dev'
        $updatedPackageDisplayName = Remove-DevSuffix -Value $packageDisplayName -Suffix ' (Dev)'
        $updatedVisualDisplayName = Remove-DevSuffix -Value $visualDisplayName -Suffix ' (Dev)'
    }

    if ($updatedIdentityName -ne $identityName) {
        Write-Host "Updated Identity Name: $identityName -> $updatedIdentityName"
    }

    $identityNode.SetAttribute('Name', $updatedIdentityName)

    if ($null -ne $displayNameNode -and $null -ne $updatedPackageDisplayName) {
        $displayNameNode.InnerText = $updatedPackageDisplayName
    }

    if ($null -ne $visualElementsNode -and $null -ne $updatedVisualDisplayName) {
        $visualElementsNode.SetAttribute('DisplayName', $updatedVisualDisplayName)
    }

    $xml.Save($Path)
}

$metadata = Get-ChannelMetadata -SourceRefName $RefName
Write-Host "Resolved channel: $($metadata.Channel)"
Write-Host "Build constants: $($metadata.BuildConstants)"

if ($metadata.UseDevBranding) {
    Apply-DevAssets
}

if ($UpdateManifest) {
    Update-ChannelManifest -Path $ManifestPath -Metadata $metadata
}

Write-GitHubKeyValue -Path $GitHubEnvPath -Name 'Build_Constants' -Value $metadata.BuildConstants
Write-GitHubKeyValue -Path $GitHubEnvPath -Name 'Build_Channel' -Value $metadata.Channel
Write-GitHubKeyValue -Path $GitHubEnvPath -Name 'Display_Version' -Value $metadata.DisplayVersion
Write-GitHubKeyValue -Path $GitHubEnvPath -Name 'Artifact_Version' -Value $metadata.ArtifactVersion
Write-GitHubKeyValue -Path $GitHubEnvPath -Name 'Package_Version' -Value $metadata.PackageVersion

Write-GitHubKeyValue -Path $GitHubOutputPath -Name 'build_constants' -Value $metadata.BuildConstants
Write-GitHubKeyValue -Path $GitHubOutputPath -Name 'channel' -Value $metadata.Channel
Write-GitHubKeyValue -Path $GitHubOutputPath -Name 'display_version' -Value $metadata.DisplayVersion
Write-GitHubKeyValue -Path $GitHubOutputPath -Name 'artifact_version' -Value $metadata.ArtifactVersion
Write-GitHubKeyValue -Path $GitHubOutputPath -Name 'package_version' -Value $metadata.PackageVersion

if ([string]::IsNullOrWhiteSpace($GitHubEnvPath) -and [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
    Write-Host "Build_Constants=$($metadata.BuildConstants)"
    Write-Host "Build_Channel=$($metadata.Channel)"
    Write-Host "Display_Version=$($metadata.DisplayVersion)"
    Write-Host "Artifact_Version=$($metadata.ArtifactVersion)"
}