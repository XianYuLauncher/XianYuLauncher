namespace XianYuLauncher.Core.Models;

/// <summary>
/// 云控公告信息模型
/// </summary>
public class AnnouncementInfo
{
    /// <summary>
    /// 公告唯一标识符（用于判断是否为新公告）
    /// </summary>
    public string id { get; set; } = string.Empty;
    
    /// <summary>
    /// 公告标题
    /// </summary>
    public string title { get; set; } = string.Empty;
    
    /// <summary>
    /// 公告内容（支持 Markdown 或纯文本）
    /// </summary>
    public string content { get; set; } = string.Empty;
    
    /// <summary>
    /// 公告类型：info, warning, error, success
    /// </summary>
    public string type { get; set; } = "info";
    
    /// <summary>
    /// 是否为重要公告（重要公告会强制显示）
    /// </summary>
    public bool important { get; set; } = false;
    
    /// <summary>
    /// 公告发布时间（ISO 8601 格式）
    /// </summary>
    public string publish_time { get; set; } = string.Empty;
    
    /// <summary>
    /// 公告过期时间（ISO 8601 格式，可选）
    /// </summary>
    public string? expire_time { get; set; }

    /// <summary>
    /// 最低可见版本（如 1.3.0，可选）
    /// </summary>
    public string? min_version { get; set; }

    /// <summary>
    /// 最高可见版本（如 1.4.0，可选）
    /// </summary>
    public string? max_version { get; set; }
    
    /// <summary>
    /// 自定义 XAML 内容（可选，如果提供则优先使用）
    /// </summary>
    public string? custom_xaml { get; set; }
    
    /// <summary>
    /// 按钮配置列表
    /// </summary>
    public List<AnnouncementButton>? buttons { get; set; }
}

/// <summary>
/// 公告按钮配置
/// </summary>
public class AnnouncementButton
{
    /// <summary>
    /// 按钮文本
    /// </summary>
    public string text { get; set; } = string.Empty;
    
    /// <summary>
    /// 按钮类型：primary, secondary, close
    /// </summary>
    public string type { get; set; } = "close";
    
    /// <summary>
    /// 按钮点击后的动作：close, open_url
    /// </summary>
    public string action { get; set; } = "close";
    
    /// <summary>
    /// 动作参数（如 URL）
    /// </summary>
    public string? action_param { get; set; }
}
