# Packaged WinUI 3 CLI run script (VS-style loose file registration)
# Usage:
#   .\run.ps1 -BuildOnly     # 仅编译验证，最快，适合 AI/CI
#   .\run.ps1 -NoLaunch      # 打包+注册，不启动
#   .\run.ps1                # 打包+注册+启动（完整 F5 流程）

param(
    [switch]$BuildOnly,  # 仅 msbuild 编译，不打包/注册/启动
    [switch]$NoLaunch    # 打包+注册，但不启动
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path $ScriptDir -Parent
Set-Location $ProjectRoot

$ProjectPath = "XianYuLauncher\XianYuLauncher.csproj"
$CsprojPath = "XianYuLauncher\XianYuLauncher.csproj"
$ManifestPath = "XianYuLauncher\Package.appxmanifest"
$AppPackagesDir = "AppPackages"
$LayoutDir = "AppPackages\XianYuLauncher_Layout"
$CertPath = "XianYuLauncher\XianYuLauncher_Dev.pfx"

if ($BuildOnly) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  XianYuLauncher - Build Only" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    msbuild $ProjectPath -p:Configuration=Debug -p:Platform=x64 -p:WarningLevel=0 -clp:ErrorsOnly
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
    $msixFile = Get-ChildItem -Path . -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match "_Test" } | Select-Object -First 1
    if (-not $msixFile) {
        Write-Host "No .msix file found in *_Test folder" -ForegroundColor Red
        exit 1
    }

    Write-Host "  Found: $($msixFile.Name)" -ForegroundColor Green

    Write-Host ""
    Write-Host "[3/4] Registering (VS-style loose file)..." -ForegroundColor Yellow
    # 若应用在运行，先结束进程，否则 Remove-Item 会因文件被占用而失败
    $running = Get-Process -Name "XianYuLauncher" -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "  Stopping running app..." -ForegroundColor Gray
        $running | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
    # Extract MSIX to layout folder (MSIX is ZIP-based)
    $layoutPath = Join-Path $PWD $LayoutDir
    if (Test-Path $layoutPath) { Remove-Item $layoutPath -Recurse -Force }
    New-Item -ItemType Directory -Path $layoutPath -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($msixFile.FullName, $layoutPath)
    Write-Host "  Extracted to layout" -ForegroundColor Gray

    # Only Remove when package is INSTALLED (from old Add-AppxPackage), not when already REGISTERED
    $existing = Get-AppxPackage -Name "SpiritStudio.XianYuLauncher" -ErrorAction SilentlyContinue |
        Where-Object { $_.Publisher -eq "CN=XianYuLauncher" } |
        Select-Object -First 1
    $configBackup = $null
    if ($existing) {
        $isRegistered = $existing.InstallLocation -and $existing.InstallLocation -like "*XianYuLauncher_Layout*"
        if (-not $isRegistered) {
            # Installed package: must Remove (loses config), backup first
            $appDataPath = "$env:LOCALAPPDATA\Packages\$($existing.PackageFamilyName)"
            if (Test-Path $appDataPath) {
                $configBackup = "$env:TEMP\XianYuLauncher_ConfigBackup_$([DateTime]::Now.Ticks)"
                Copy-Item -Path $appDataPath -Destination $configBackup -Recurse -Force
                Write-Host "  Backed up config (migrating from installed)" -ForegroundColor Gray
            }
            Write-Host "  Removing installed package..." -ForegroundColor Gray
            Remove-AppxPackage -Package $existing.PackageFullName
        }
    }

    $manifestPath = Join-Path $layoutPath "AppxManifest.xml"
    Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown
    Write-Host "  Registered" -ForegroundColor Green

    if ($configBackup -and (Test-Path $configBackup)) {
        $pkgNew = Get-AppxPackage -Name "SpiritStudio.XianYuLauncher" -ErrorAction SilentlyContinue |
            Where-Object { $_.Publisher -eq "CN=XianYuLauncher" } |
            Select-Object -First 1
        if ($pkgNew) {
            $appDataNew = "$env:LOCALAPPDATA\Packages\$($pkgNew.PackageFamilyName)"
            Copy-Item -Path "$configBackup\*" -Destination $appDataNew -Recurse -Force
            Remove-Item $configBackup -Recurse -Force
            Write-Host "  Restored config" -ForegroundColor Gray
        }
    }

    if (-not $NoLaunch) {
        Write-Host ""
        Write-Host "[4/4] Launching..." -ForegroundColor Yellow
        $pkg = Get-AppxPackage -Name "SpiritStudio.XianYuLauncher" |
            Where-Object { $_.Publisher -eq "CN=XianYuLauncher" } |
            Sort-Object Version -Descending |
            Select-Object -First 1
        if (-not $pkg) {
            $pkg = Get-AppxPackage -Name "SpiritStudio.XianYuLauncher" |
                Sort-Object Version -Descending |
                Select-Object -First 1
        }
        if (-not $pkg) {
            Write-Host "  Launch failed: package not found." -ForegroundColor Red
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
