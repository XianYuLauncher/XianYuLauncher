param(
    [Parameter(Mandatory = $true)]
    [string]$AppId,

    [Parameter(Mandatory = $true)]
    [string]$TenantId,

    [Parameter(Mandatory = $true)]
    [string]$ClientId,

    [Parameter(Mandatory = $true)]
    [string]$ClientSecret,

    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,

    [string]$OutputDirectory = "artifacts/store-submission-payload",

    [string]$OutName = "StoreSubmission"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-NotEmpty {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name cannot be empty."
    }
}

function Connect-StoreBroker {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TenantId,

        [Parameter(Mandatory = $true)]
        [string]$ClientId,

        [Parameter(Mandatory = $true)]
        [string]$ClientSecret
    )

    $secureClientSecret = ConvertTo-SecureString -String $ClientSecret -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential ($ClientId, $secureClientSecret)
    Set-StoreBrokerAuthentication -TenantId $TenantId -Credential $credential | Out-Null
}

Assert-NotEmpty -Name 'AppId' -Value $AppId
Assert-NotEmpty -Name 'TenantId' -Value $TenantId
Assert-NotEmpty -Name 'ClientId' -Value $ClientId
Assert-NotEmpty -Name 'ClientSecret' -Value $ClientSecret

if (-not (Test-Path -Path $PackageDirectory -PathType Container)) {
    throw "PackageDirectory '$PackageDirectory' does not exist."
}

Import-Module StoreBroker -ErrorAction Stop

$outputFullPath = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputFullPath | Out-Null

$configPath = Join-Path $outputFullPath 'storebroker.config.json'
$payloadPath = Join-Path $outputFullPath ($OutName + '.json')
$zipPath = Join-Path $outputFullPath ($OutName + '.zip')
$manifestPath = Join-Path $outputFullPath 'store-submission-package-manifest.json'

Remove-Item -Path $configPath, $payloadPath, $zipPath, $manifestPath -Force -ErrorAction SilentlyContinue

$packageFiles = @(Get-ChildItem -Path $PackageDirectory -File -Recurse | Where-Object {
    $_.Extension -in '.appx', '.appxbundle', '.appxupload', '.msix', '.msixbundle', '.msixupload'
} | Sort-Object FullName)

if ($packageFiles.Count -eq 0) {
    throw "No Store package files were found under '$PackageDirectory'."
}

Connect-StoreBroker -TenantId $TenantId -ClientId $ClientId -ClientSecret $ClientSecret
New-StoreBrokerConfigFile -AppId $AppId -Path $configPath

New-SubmissionPackage `
    -ConfigPath $configPath `
    -AppxPath $packageFiles.FullName `
    -OutPath $outputFullPath `
    -OutName $OutName `
    -DisableAutoPackageNameFormatting

if (-not (Test-Path -Path $payloadPath -PathType Leaf)) {
    throw "Expected submission payload '$payloadPath' was not created."
}

if (-not (Test-Path -Path $zipPath -PathType Leaf)) {
    throw "Expected submission package '$zipPath' was not created."
}

$payload = Get-Content -Path $payloadPath -Raw | ConvertFrom-Json
$generatedPackages = @($payload.applicationPackages)
if ($generatedPackages.Count -eq 0) {
    throw "The generated submission payload does not contain any applicationPackages entries."
}

$manifest = [ordered]@{
    appId = $AppId
    packageDirectory = [System.IO.Path]::GetFullPath($PackageDirectory)
    payloadJsonFile = [System.IO.Path]::GetFileName($payloadPath)
    payloadZipFile = [System.IO.Path]::GetFileName($zipPath)
    sourcePackages = @($packageFiles | ForEach-Object { $_.Name })
    generatedPackages = @(
        foreach ($package in $generatedPackages) {
            [ordered]@{
                fileName = [string]$package.fileName
                fileStatus = [string]$package.fileStatus
            }
        }
    )
}

$encoding = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 5), $encoding)

Write-Host "Store submission payload created at $payloadPath"
Write-Host "Store submission package created at $zipPath"