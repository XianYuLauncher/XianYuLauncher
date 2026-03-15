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

$utf8 = [System.Text.Encoding]::UTF8
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
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
    [System.IO.File]::WriteAllText($csprojPath, $csproj, $utf8NoBom)
    Write-Host "(csproj) 已写入变更"
}
else {
    Write-Host "(csproj) 无变更，跳过写入"
}

if ($originalManifest -ne $manifest) {
    [System.IO.File]::WriteAllText($manifestPath, $manifest, $utf8NoBom)
    Write-Host "(manifest) 已写入变更"
}
else {
    Write-Host "(manifest) 无变更，跳过写入"
}

Write-Host "========================================"
Write-Host "  完成! 现在可以打包了"
Write-Host "========================================"
