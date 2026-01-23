namespace XianYuLauncher.Core.Models;

/// <summary>
/// 崩溃分析结果
/// </summary>
public class CrashAnalysisResult
{
    /// <summary>
    /// 简短标题（用于弹窗显示）
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 崩溃分析结果
    /// </summary>
    public string Analysis { get; set; } = "未知崩溃原因";
    
    /// <summary>
    /// 诊断建议列表
    /// </summary>
    public List<string> Suggestions { get; set; } = new();
    
    /// <summary>
    /// 崩溃类型
    /// </summary>
    public CrashType Type { get; set; } = CrashType.Unknown;

    /// <summary>
    /// 可选的修复操作
    /// </summary>
    public CrashFixAction? FixAction { get; set; }
}

/// <summary>
/// 崩溃修复操作
/// </summary>
public class CrashFixAction
{
    /// <summary>
    /// 操作类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 按钮显示文本
    /// </summary>
    public string ButtonText { get; set; } = string.Empty;

    /// <summary>
    /// 操作参数
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>
/// 崩溃类型枚举
/// </summary>
public enum CrashType
{
    /// <summary>
    /// 未知崩溃
    /// </summary>
    Unknown,
    
    /// <summary>
    /// 手动触发的崩溃（调试用）
    /// </summary>
    ManuallyTriggered,
    
    /// <summary>
    /// 内存不足
    /// </summary>
    OutOfMemory,
    
    /// <summary>
    /// Mod 冲突
    /// </summary>
    ModConflict,
    
    /// <summary>
    /// 缺少依赖
    /// </summary>
    MissingDependency,
    
    /// <summary>
    /// Java 版本不匹配
    /// </summary>
    JavaVersionMismatch,
    
    /// <summary>
    /// 图形驱动问题
    /// </summary>
    GraphicsDriver,
    
    /// <summary>
    /// 网络错误
    /// </summary>
    NetworkError,
    
    /// <summary>
    /// 存档损坏
    /// </summary>
    CorruptedWorld
}
