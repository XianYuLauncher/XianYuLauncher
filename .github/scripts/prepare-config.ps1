# ========================================
# CI - 配置文件预处理脚本
# ========================================
# 用途：处理应用配置文件格式转换

param(
    [string]$InputFile = "XianYuLauncher\secrets.json",
    [string]$OutputFile = "XianYuLauncher\secrets.enc",
    [string]$ConfigKey = $env:AES_ENCRYPTION_KEY
)

Write-Host "CI: 正在处理配置文件 ..."

# 检查输入文件
if (-not (Test-Path $InputFile)) {
    Write-Error "找不到配置文件 $InputFile"
    exit 1
}

# 检查处理密钥
if ([string]::IsNullOrWhiteSpace($ConfigKey)) {
    Write-Error "配置处理密钥未设置"
    exit 1
}

# 读取配置
$configData = Get-Content $InputFile -Raw -Encoding UTF8

# 处理配置数据
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
    
    Write-Host "✓ 配置处理完成: $OutputFile"
    
    # 清理临时文件
    Remove-Item $InputFile -Force
    Write-Host "✓ 已清理临时文件: $InputFile"
    
    $transformer.Dispose()
    $processor.Dispose()
}
catch {
    Write-Error "配置处理失败: $_"
    exit 1
}
