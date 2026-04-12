# 本脚本仅用于本地维护历史 Packaged/MSIX 分发配置。
# CI/CD 主线已改为 Store 走 MSIX、SideLoad 走 Unpackaged Zip，不再调用本脚本。
# 切换到商店模式: .\switch-publish-mode.ps1 -Mode Store
# 切换到侧载模式: .\switch-publish-mode.ps1 -Mode Sideload

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Store', 'Sideload')]
    [string]$Mode
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csprojPath = Join-Path $scriptDir 'XianYuLauncher.csproj'
$manifestPath = Join-Path $scriptDir 'Package.appxmanifest'

$storePublisher = 'CN=477122EB-593B-4C14-AA43-AD408DEE1452'
$sideloadPublisher = 'CN=XianYuLauncher'

Write-Host "========================================"
Write-Host "  切换发布模式: $Mode"
Write-Host "========================================"
Write-Warning '此脚本不再参与 SideLoad 主线发布，仅供维护历史 MSIX/AppInstaller 配置时使用。'

$utf8 = [System.Text.Encoding]::UTF8

function Get-HasUtf8Bom {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $false }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    return $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
}

function Write-TextPreserveBom {
    param(
        [string]$Path,
        [string]$Content,
        [bool]$UseBom
    )
    $encoding = [System.Text.UTF8Encoding]::new($UseBom)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

$csprojHasBom = Get-HasUtf8Bom -Path $csprojPath
$manifestHasBom = Get-HasUtf8Bom -Path $manifestPath

$csproj = [System.IO.File]::ReadAllText($csprojPath, $utf8)
$manifest = [System.IO.File]::ReadAllText($manifestPath, $utf8)
$originalCsproj = $csproj
$originalManifest = $manifest

if ($Mode -eq 'Store')
{
    Write-Host "(csproj) 切换到商店模式..."
    $csproj = $csproj -replace '<GenerateTemporaryStoreCertificate>True</GenerateTemporaryStoreCertificate>', '<GenerateTemporaryStoreCertificate>False</GenerateTemporaryStoreCertificate>'
    $csproj = $csproj -replace '(\s*)(<PackageCertificateThumbprint>)', '$1<!--$2'
    $csproj = $csproj -replace '(</PackageCertificateThumbprint>)(\s*)', '$1-->$2'
    $csproj = $csproj -replace '<!--<!--', '<!--'
    $csproj = $csproj -replace '-->-->', '-->'

    Write-Host "(manifest) 切换发布者到商店..."
    $manifest = $manifest -replace 'Publisher="CN=XianYuLauncher"', ('Publisher="' + $storePublisher + '"')

    Write-Host "商店模式配置完成!"
}
else
{
    Write-Host "(csproj) 切换到侧载模式..."
    $csproj = $csproj -replace '<GenerateTemporaryStoreCertificate>False</GenerateTemporaryStoreCertificate>', '<GenerateTemporaryStoreCertificate>True</GenerateTemporaryStoreCertificate>'
    $csproj = $csproj -replace '<!--(<PackageCertificateThumbprint>)', '$1'
    $csproj = $csproj -replace '(</PackageCertificateThumbprint>)-->', '$1'

    Write-Host "(manifest) 切换发布者到侧载..."
    $manifest = $manifest -replace ('Publisher="' + $storePublisher + '"'), 'Publisher="CN=XianYuLauncher"'

    Write-Host "侧载模式配置完成!"
}

if ($originalCsproj -ne $csproj) {
    Write-TextPreserveBom -Path $csprojPath -Content $csproj -UseBom $csprojHasBom
    Write-Host "(csproj) 已写入变更"
}
else {
    Write-Host "(csproj) 无变更，跳过写入"
}

if ($originalManifest -ne $manifest) {
    Write-TextPreserveBom -Path $manifestPath -Content $manifest -UseBom $manifestHasBom
    Write-Host "(manifest) 已写入变更"
}
else {
    Write-Host "(manifest) 无变更，跳过写入"
}

Write-Host "========================================"
Write-Host "  完成! 现在可以打包了"
Write-Host "========================================"
