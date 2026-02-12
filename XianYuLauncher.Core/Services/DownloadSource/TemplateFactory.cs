using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// 模板工厂，用于创建下载源模板实例
/// </summary>
public static class TemplateFactory
{
    private static readonly Dictionary<DownloadSourceTemplateType, DownloadSourceTemplate> _templates = new()
    {
        { DownloadSourceTemplateType.Official, new BmclapiTemplate() },
        { DownloadSourceTemplateType.Community, new McimTemplate() }
    };

    /// <summary>
    /// 获取指定类型的模板实例
    /// </summary>
    /// <param name="type">模板类型</param>
    /// <returns>模板实例</returns>
    /// <exception cref="ArgumentException">未知的模板类型</exception>
    public static DownloadSourceTemplate GetTemplate(DownloadSourceTemplateType type)
    {
        if (_templates.TryGetValue(type, out var template))
        {
            return template;
        }
        throw new ArgumentException($"Unknown template type: {type}");
    }

    /// <summary>
    /// 获取所有可用的模板
    /// </summary>
    /// <returns>所有模板的集合</returns>
    public static IEnumerable<DownloadSourceTemplate> GetAllTemplates()
    {
        return _templates.Values;
    }
}
