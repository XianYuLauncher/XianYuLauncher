namespace XianYuLauncher.Models;

/// <summary>
/// 自动检查更新模式。
/// </summary>
public enum AutoUpdateCheckModeType
{
    /// <summary>
    /// 每次启动检查。
    /// </summary>
    Always,

    /// <summary>
    /// 仅重要更新时提示。
    /// </summary>
    ImportantOnly
}
