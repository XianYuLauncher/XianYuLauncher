# 替换命名空间脚本
# 从 XMCL2025 替换为 XianYuLauncher
# 兼容 PowerShell 5

$projectRoot = "c:\Users\pc\source\repos\XMCL2025\XianYuLauncher"
$oldNamespace = "XMCL2025"
$newNamespace = "XianYuLauncher"

# 要替换的文件类型
$fileTypes = @("*.cs", "*.xaml", "*.csproj", "*.appxmanifest")

# 要排除的目录
$excludeDirs = @("bin", "obj", ".trae", ".vscode")

Write-Host "开始替换命名空间..."
Write-Host "项目根目录: $projectRoot"
Write-Host "旧命名空间: $oldNamespace"
Write-Host "新命名空间: $newNamespace"
Write-Host "" 

# 遍历所有文件
$totalFiles = 0
$modifiedFiles = 0

foreach ($fileType in $fileTypes) {
    Write-Host "正在处理 $fileType 文件..."
    
    # 获取所有匹配的文件，排除指定目录
    $files = Get-ChildItem -Path $projectRoot -Recurse -Filter $fileType | Where-Object {
        $exclude = $false
        foreach ($dir in $excludeDirs) {
            if ($_.DirectoryName -like "*$dir*") {
                $exclude = $true
                break
            }
        }
        -not $exclude
    }
    
    $totalFiles += $files.Count
    
    foreach ($file in $files) {
        Write-Host "处理文件: $($file.FullName)"
        
        # 读取文件内容
        $content = Get-Content -Path $file.FullName -Raw
        
        # 检查是否需要替换
        if ($content -match $oldNamespace) {
            # 替换命名空间
            $newContent = $content -replace $oldNamespace, $newNamespace
            
            # 写入文件
            Set-Content -Path $file.FullName -Value $newContent -NoNewline
            
            $modifiedFiles++
            Write-Host "  ✅ 已修改"
        } else {
            Write-Host "  ⏭️  不需要修改"
        }
    }
    
    Write-Host ""
}

Write-Host "替换完成!"
Write-Host "总文件数: $totalFiles"
Write-Host "修改文件数: $modifiedFiles"
Write-Host ""