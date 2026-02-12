using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Models;

/// <summary>
/// 自定义下载源 UI 数据模型
/// </summary>
public partial class CustomSourceViewModel : ObservableObject
{
    /// <summary>
    /// 唯一标识键
    /// </summary>
    [ObservableProperty]
    private string _key = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 基础 URL
    /// </summary>
    [ObservableProperty]
    private string _baseUrl = string.Empty;

    /// <summary>
    /// 模板类型
    /// </summary>
    [ObservableProperty]
    private DownloadSourceTemplateType _template;

    /// <summary>
    /// 是否启用
    /// </summary>
    [ObservableProperty]
    private bool _enabled;

    /// <summary>
    /// 优先级
    /// </summary>
    [ObservableProperty]
    private int _priority = 100;

    /// <summary>
    /// 模板类型显示名称
    /// </summary>
    public string TemplateDisplayName => Template switch
    {
        DownloadSourceTemplateType.Official => "官方资源",
        DownloadSourceTemplateType.Community => "社区资源",
        _ => Template.ToString()
    };

    /// <summary>
    /// 从 Core 模型创建 ViewModel
    /// </summary>
    public static CustomSourceViewModel FromCoreModel(CustomSource source)
    {
        Enum.TryParse<DownloadSourceTemplateType>(source.Template, true, out var templateType);

        return new CustomSourceViewModel
        {
            Key = source.Key,
            Name = source.Name,
            BaseUrl = source.BaseUrl,
            Template = templateType,
            Enabled = source.Enabled,
            Priority = source.Priority
        };
    }

    /// <summary>
    /// 转换为 Core 模型
    /// </summary>
    public CustomSource ToCoreModel()
    {
        return new CustomSource
        {
            Key = Key,
            Name = Name,
            BaseUrl = BaseUrl,
            Template = Template.ToString().ToLowerInvariant(),
            Enabled = Enabled,
            Priority = Priority,
            Overrides = null
        };
    }
}
