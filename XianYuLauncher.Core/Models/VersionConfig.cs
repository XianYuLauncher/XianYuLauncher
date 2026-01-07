using System;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 版本配置文件模型，存储版本的ModLoader信息
/// </summary>
public class VersionConfig
{
    /// <summary>
    /// ModLoader类型（fabric, neoforge, forge, vanilla）
    /// </summary>
    public string ModLoaderType { get; set; } = string.Empty;
    
    /// <summary>
    /// ModLoader版本号（完整版本，如21.11.0-beta）
    /// </summary>
    public string ModLoaderVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Minecraft版本号
    /// </summary>
    public string MinecraftVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Optifine版本号（如果安装了Optifine）
    /// </summary>
    public string? OptifineVersion { get; set; }
    
    /// <summary>
    /// 配置文件创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 是否自动分配内存
    /// </summary>
    public bool AutoMemoryAllocation { get; set; } = true;
    
    /// <summary>
    /// 初始堆内存（GB）
    /// </summary>
    public double InitialHeapMemory { get; set; } = 6.0;
    
    /// <summary>
    /// 最大堆内存（GB）
    /// </summary>
    public double MaximumHeapMemory { get; set; } = 12.0;
    
    /// <summary>
    /// Java路径
    /// </summary>
    public string JavaPath { get; set; } = string.Empty;
    
    /// <summary>
    /// 窗口宽度
    /// </summary>
    public int WindowWidth { get; set; } = 1280;
    
    /// <summary>
    /// 窗口高度
    /// </summary>
    public int WindowHeight { get; set; } = 720;
}
