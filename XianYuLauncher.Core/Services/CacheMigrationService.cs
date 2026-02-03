using System;
using System.Diagnostics;
using System.IO;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 缓存迁移服务，用于将旧版本的虚拟化路径数据迁移到新的安全路径
/// </summary>
public static class CacheMigrationService
{
    /// <summary>
    /// 执行迁移（仅在 MSIX 环境下）
    /// 将 LocalCache\Local\XianYuLauncher\ 迁移到 LocalState\
    /// </summary>
    public static void MigrateIfNeeded()
    {
        if (!Helpers.AppEnvironment.IsMSIX)
        {
            return;
        }

        try
        {
            // 获取新路径 (LocalState)
            string newPath = Helpers.AppEnvironment.SafeAppDataPath;
            
            // 构建旧路径 (LocalCache\Local\XianYuLauncher)
            string packageRoot = Path.GetDirectoryName(newPath)!;
            string oldPath = Path.Combine(packageRoot, "LocalCache", "Local", "XianYuLauncher");

            if (!Directory.Exists(oldPath))
            {
                Debug.WriteLine($"[CacheMigration] 旧路径不存在，无需迁移: {oldPath}");
                return;
            }

            Debug.WriteLine($"[CacheMigration] 检测到旧版本数据，开始迁移...");
            Debug.WriteLine($"[CacheMigration] 旧路径: {oldPath}");
            Debug.WriteLine($"[CacheMigration] 新路径: {newPath}");

            // 清空新目录（如果存在）
            if (Directory.Exists(newPath))
            {
                Directory.Delete(newPath, true);
                Debug.WriteLine($"[CacheMigration] 已清空新目录");
            }

            // 直接移动目录
            Directory.Move(oldPath, newPath);
            Debug.WriteLine($"[CacheMigration] 迁移完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CacheMigration] 迁移失败: {ex.Message}");
        }
    }
}
