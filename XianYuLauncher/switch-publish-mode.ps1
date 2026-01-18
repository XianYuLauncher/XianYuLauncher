# 设置编码为 UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['Out-File:Encoding'] = 'utf8'

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Store", "Sideload")]
    [string]$Mode
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csprojPath = Join-Path $scriptDir "XianYuLauncher.csproj"
$manifestPath = Join-Path $scriptDir "Package.appxmanifest"

# 配置信息
$storePublisher = "CN=477122EB-593B-4C14-AA43-AD408DEE1452"
$sideloadPublisher = "CN=XianYuLauncher"
$certificateThumbprint = "327A38EBE17C4905857F79FF203FE85A32C3EBA0"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  切换发布模式: $Mode" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 读取 csproj
$csproj = Get-Content $csprojPath -Raw -Encoding UTF8

# 读取 manifest
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8

if ($Mode -eq "Store") {
    Write-Host "`n[csproj] 切换到商店模式..." -ForegroundColor Yellow
    
    # GenerateTemporaryStoreCertificate -> False
    $csproj = $csproj -replace '<GenerateTemporaryStoreCertificate>True</GenerateTemporaryStoreCertificate>', '<GenerateTemporaryStoreCertificate>False</GenerateTemporaryStoreCertificate>'
    
    # 注释掉证书指纹（如果没注释的话）
    $csproj = $csproj -replace '(\s*)(<PackageCertificateThumbprint>)', '$1<!--$2'
    $csproj = $csproj -replace '(</PackageCertificateThumbprint>)(\s*\r?\n)', '$1-->$2'
    
    $csproj = $csproj -replace '<!--<!--', '<!--'
    $csproj = $csproj -replace '-->-->', '-->'
    
    Write-Host "[manifest] 切换发布者到商店..." -ForegroundColor Yellow
    $manifest = $manifest -replace 'Publisher="CN=XianYuLauncher"', "Publisher=`"$storePublisher`""
    
    Write-Host "`n商店模式配置完成!" -ForegroundColor Green
    Write-Host "  - GenerateTemporaryStoreCertificate: False" -ForegroundColor Gray
    Write-Host "  - PackageCertificateThumbprint: 已注释" -ForegroundColor Gray
    Write-Host "  - Publisher: $storePublisher" -ForegroundColor Gray
}
else {
    Write-Host "`n[csproj] 切换到侧载..." -ForegroundColor Yellow
    
    # GenerateTemporaryStoreCertificate -> True
    $csproj = $csproj -replace '<GenerateTemporaryStoreCertificate>False</GenerateTemporaryStoreCertificate>', '<GenerateTemporaryStoreCertificate>True</GenerateTemporaryStoreCertificate>'
    
    # 取消注释证书指纹
    $csproj = $csproj -replace '<!--(<PackageCertificateThumbprint>)', '$1'
    $csproj = $csproj -replace '(</PackageCertificateThumbprint>)-->', '$1'
    
    Write-Host "[manifest] 切换发布者到侧载..." -ForegroundColor Yellow
    $manifest = $manifest -replace "Publisher=`"$storePublisher`"", 'Publisher="CN=XianYuLauncher"'
    
    Write-Host "`n侧载模式配置完成!" -ForegroundColor Green
    Write-Host "  - GenerateTemporaryStoreCertificate: True" -ForegroundColor Gray
    Write-Host "  - PackageCertificateThumbprint: $certificateThumbprint" -ForegroundColor Gray
    Write-Host "  - Publisher: $sideloadPublisher" -ForegroundColor Gray
}

# 写回文件
$utf8Bom = New-Object System.Text.UTF8Encoding $true
[System.IO.File]::WriteAllText($csprojPath, $csproj, $utf8Bom)
[System.IO.File]::WriteAllText($manifestPath, $manifest, $utf8Bom)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  完成! 现在可以打包了" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
