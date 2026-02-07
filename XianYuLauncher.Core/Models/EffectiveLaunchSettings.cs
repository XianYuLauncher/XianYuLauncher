namespace XianYuLauncher.Core.Models;

/// <summary>
/// 最终生效的启动设置，由全局设置和版本设置合并而来
/// </summary>
public class EffectiveLaunchSettings
{
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
    /// Java路径（已解析，可直接使用）
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
