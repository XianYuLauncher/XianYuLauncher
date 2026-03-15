# Packaged WinUI 3 CLI run script (VS-style loose file registration)
# Usage:
#   .\run.ps1 -BuildOnly              # 仅编译验证，最快，适合 AI/CI
#   .\run.ps1 -NoLaunch               # 打包+注册，不启动
#   .\run.ps1                         # 打包+注册+启动（完整 F5 流程）
#   .\run.ps1 -Channel Dev            # Dev 通道（独立包名/图标/编译宏）

param(
    [switch]$BuildOnly,  # 仅 msbuild 编译，不打包/注册/启动
    [switch]$NoLaunch,   # 打包+注册，但不启动
    [ValidateSet('Stable', 'Dev')]
    [string]$Channel = 'Stable'
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path $ScriptDir -Parent
Set-Location $ProjectRoot

$ProjectPath = "XianYuLauncher\XianYuLauncher.csproj"
$AppPackagesDir = "AppPackages"
$LayoutDir = "AppPackages\XianYuLauncher_Layout"
$CertPath = "XianYuLauncher\XianYuLauncher_Dev.pfx"
$DevAssetsDir = "XianYuLauncher\Assets\Dev"
$ChannelConstant = if ($Channel -eq 'Dev') { 'DEV_CHANNEL' } else { 'RELEASE_CHANNEL' }

function Apply-DevBrandingToLayout {
    param(
        [string]$LayoutManifestPath,
        [string]$LayoutAssetsDir,
        [string]$DevAssetsDir
    )

    [xml]$xml = Get-Content -Path $LayoutManifestPath -Encoding UTF8

    $oldName = $xml.Package.Identity.Name
    if (-not $oldName.EndsWith('.Dev')) {
        $xml.Package.Identity.Name = "$oldName.Dev"
    }

    $xml.Package.Properties.DisplayName = 'XianYu Launcher (Dev)'
    if ($xml.Package.Applications.Application.VisualElements) {
        $xml.Package.Applications.Application.VisualElements.DisplayName = 'XianYu Launcher (Dev)'
    }

    $xml.Save($LayoutManifestPath)
    Write-Host "  Applied dev branding to layout manifest" -ForegroundColor Gray

    if (Test-Path $DevAssetsDir) {
        $devFiles = Get-ChildItem -Path $DevAssetsDir -File
        foreach ($file in $devFiles) {
            Copy-Item -Path $file.FullName -Destination (Join-Path $LayoutAssetsDir $file.Name) -Force
            Write-Host "  Replaced layout asset: $($file.Name)" -ForegroundColor Gray
        }
    } else {
        Write-Host "  Dev assets folder not found: $DevAssetsDir" -ForegroundColor Yellow
    }
}

if ($BuildOnly) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  XianYuLauncher - Build Only" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Channel: $Channel ($ChannelConstant)" -ForegroundColor Gray
    msbuild $ProjectPath -p:Configuration=Debug -p:Platform=x64 -p:DefineConstants=$ChannelConstant -p:WarningLevel=0 -clp:ErrorsOnly
    if ($LASTEXITCODE -eq 0) { Write-Host "Build succeeded (exit 0)" -ForegroundColor Green }
    exit $LASTEXITCODE
}

# 1. Switch to Sideload (dev cert needs Publisher=CN=XianYuLauncher)
& "$ProjectRoot\XianYuLauncher\switch-publish-mode.ps1" -Mode Sideload | Out-Null

# 2. Create dev cert if not exists
if (-not (Test-Path $CertPath)) {
    Write-Host "Creating dev certificate..." -ForegroundColor Gray
    $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=XianYuLauncher" -KeyUsage DigitalSignature -FriendlyName "XianYuLauncher Dev" -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5)
    $pwd = New-Object System.Security.SecureString
    Export-PfxCertificate -Cert $cert -FilePath $CertPath -Password $pwd | Out-Null
    Write-Host "  Created: $CertPath" -ForegroundColor Green
}

try {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  XianYuLauncher - Packaged Run" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Channel: $Channel ($ChannelConstant)" -ForegroundColor Gray

    Write-Host ""
    Write-Host "[1/4] Building MSIX..." -ForegroundColor Yellow
    msbuild $ProjectPath `
        -p:Configuration=Debug `
        -p:Platform=x64 `
        -p:GenerateAppxPackageOnBuild=true `
        -p:UapAppxPackageBuildMode=SideloadOnly `
        -p:AppxBundle=Never `
        -p:PackageCertificateKeyFile="$PWD\$CertPath" `
        -p:AppxPackageSigningEnabled=true `
        -p:DefineConstants=$ChannelConstant `
        -p:AppxPackageDir="$PWD\$AppPackagesDir\" `
        -p:PackageOutputPath="$PWD\AppPackages" `
        -p:WarningLevel=0 `
        -clp:ErrorsOnly

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "  Build succeeded (exit 0)" -ForegroundColor Green

    Write-Host ""
    Write-Host "[2/4] Finding MSIX..." -ForegroundColor Yellow
    $appPackagesPath = Join-Path $PWD $AppPackagesDir
    $msixFile = Get-ChildItem -Path $appPackagesPath -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "_Test" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $msixFile) {
        Write-Host "No .msix file found in *_Test folder" -ForegroundColor Red
        exit 1
    }

    Write-Host "  Found: $($msixFile.Name)" -ForegroundColor Green

    Write-Host ""
    Write-Host "[3/4] Registering (VS-style loose file)..." -ForegroundColor Yellow

    $running = Get-Process -Name "XianYuLauncher" -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "  Stopping running app..." -ForegroundColor Gray
        $running | Stop-Process -Force
        Start-Sleep -Seconds 2
    }

    $layoutPath = Join-Path $PWD $LayoutDir
    if (Test-Path $layoutPath) { Remove-Item $layoutPath -Recurse -Force }
    New-Item -ItemType Directory -Path $layoutPath -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($msixFile.FullName, $layoutPath)
    Write-Host "  Extracted to layout" -ForegroundColor Gray

    $layoutManifestPath = Join-Path $layoutPath "AppxManifest.xml"
    if ($Channel -eq 'Dev') {
        $layoutAssetsDir = Join-Path $layoutPath "Assets"
        Apply-DevBrandingToLayout -LayoutManifestPath $layoutManifestPath -LayoutAssetsDir $layoutAssetsDir -DevAssetsDir $DevAssetsDir
    }

    $targetName = if ($Channel -eq 'Dev') { "SpiritStudio.XianYuLauncher.Dev" } else { "SpiritStudio.XianYuLauncher" }
    $localPackages = Get-AppxPackage -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -eq $targetName -and
            $_.InstallLocation -and
            ($_.InstallLocation -like "$($PWD.Path)*" -or $_.InstallLocation -like "*$LayoutDir*")
        }

    foreach ($pkg in $localPackages) {
        Write-Host "  Removing old local package: $($pkg.PackageFullName)" -ForegroundColor Gray
        Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue
    }

    Add-AppxPackage -Register $layoutManifestPath -ForceApplicationShutdown
    Write-Host "  Registered" -ForegroundColor Green

    if (-not $NoLaunch) {
        Write-Host ""
        Write-Host "[4/4] Launching..." -ForegroundColor Yellow
        $pkg = Get-AppxPackage -Name $targetName -ErrorAction SilentlyContinue |
            Where-Object { $_.Publisher -eq "CN=XianYuLauncher" } |
            Sort-Object Version -Descending |
            Select-Object -First 1

        if (-not $pkg) {
            Write-Host "  Launch failed: package not found for channel $Channel." -ForegroundColor Red
            exit 1
        }

        $aumid = "$($pkg.PackageFamilyName)!App"
        Start-Process "explorer.exe" "shell:AppsFolder\$aumid"
        Write-Host "  Launched" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "[4/4] Skipped launch (-NoLaunch)" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "Done!" -ForegroundColor Cyan
} finally {
    # Restore Store mode (for release builds)
    & "$ProjectRoot\XianYuLauncher\switch-publish-mode.ps1" -Mode Store | Out-Null
}
