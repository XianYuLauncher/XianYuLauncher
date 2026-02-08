# ========================================
# CI - Configuration Preprocessing Script
# ========================================
# Purpose: Process application configuration file format conversion

param(
    [string]$InputFile = "XianYuLauncher\secrets.json",
    [string]$OutputFile = "XianYuLauncher\secrets.enc",
    [string]$ConfigKey = $env:AES_ENCRYPTION_KEY
)

Write-Host "CI: Processing configuration file ..."

# Check input file
if (-not (Test-Path $InputFile)) {
    Write-Error "Configuration file not found: $InputFile"
    exit 1
}

# Check processing key
if ([string]::IsNullOrWhiteSpace($ConfigKey)) {
    Write-Error "Configuration processing key not set"
    exit 1
}

# Read configuration
$configData = Get-Content $InputFile -Raw -Encoding UTF8

# Process configuration data
try {
    $processor = [System.Security.Cryptography.Aes]::Create()
    $processor.KeySize = 256
    $processor.Key = [Convert]::FromBase64String($ConfigKey)
    $processor.IV = New-Object byte[] 16
    
    $transformer = $processor.CreateEncryptor()
    $inputBytes = [System.Text.Encoding]::UTF8.GetBytes($configData)
    $outputBytes = $transformer.TransformFinalBlock($inputBytes, 0, $inputBytes.Length)
    
    $processedData = [Convert]::ToBase64String($outputBytes)
    $processedData | Out-File -FilePath $OutputFile -Encoding UTF8 -NoNewline
    
    Write-Host "Configuration processing completed: $OutputFile"
    
    # Clean up temporary files
    Remove-Item $InputFile -Force
    Write-Host "Temporary file cleaned: $InputFile"
    
    $transformer.Dispose()
    $processor.Dispose()
}
catch {
    Write-Error "Configuration processing failed: $_"
    exit 1
}
