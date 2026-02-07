namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 版本设置数据模型，用于存储XianYuL.cfg中的配置信息
/// </summary>
public class VersionSettings
{
    /// <summary>
    /// ModLoader类型（fabric, neoforge, forge）
    /// </summary>
    public string ModLoaderType { get; set; }
    
    /// <summary>
    /// ModLoader版本号
    /// </summary>
    public string ModLoaderVersion { get; set; }
    
    /// <summary>
    /// Optifine版本号
    /// </summary>
    public string? OptifineVersion { get; set; }

    /// <summary>
    /// Minecraft版本号
    /// </summary>
    public string MinecraftVersion { get; set; }
    
    /// <summary>
    /// 配置文件创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 是否覆盖全局内存设置（false = 跟随全局）
    /// </summary>
    public bool OverrideMemory { get; set; } = false;
    
    /// <summary>
    /// 是否自动分配内存
    /// </summary>
    public bool AutoMemoryAllocation { get; set; } = true;
    
    /// <summary>
    /// 初始堆内存（GB）
    /// </summary>
    public double InitialHeapMemory { get; set; } = 6;
    
    /// <summary>
    /// 最大堆内存（GB）
    /// </summary>
    public double MaximumHeapMemory { get; set; } = 12;
    
    /// <summary>
    /// Java路径
    /// </summary>
    public string JavaPath { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否使用全局Java设置
    /// </summary>
    public bool UseGlobalJavaSetting { get; set; } = true;
    
    /// <summary>
    /// 是否覆盖全局分辨率设置（false = 跟随全局）
    /// </summary>
    public bool OverrideResolution { get; set; } = false;
    
    /// <summary>
    /// 启动窗口宽度
    /// </summary>
    public int WindowWidth { get; set; } = 1280;
    
    /// <summary>
    /// 启动窗口高度
    /// </summary>
    public int WindowHeight { get; set; } = 720;
    
    /// <summary>
    /// 启动次数
    /// </summary>
    public int LaunchCount { get; set; } = 0;
    
    /// <summary>
    /// 总游戏时长（秒）
    /// </summary>
    public long TotalPlayTimeSeconds { get; set; } = 0;
    
    /// <summary>
    /// 最后启动时间
    /// </summary>
    public DateTime? LastLaunchTime { get; set; }
    
    /// <summary>
    /// 自定义 JVM 参数
    /// </summary>
    public string CustomJvmArguments { get; set; } = string.Empty;
}
