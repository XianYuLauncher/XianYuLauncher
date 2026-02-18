using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// 资源转移类型枚举
/// </summary>
public enum ResourceMoveType
{
    Mod,
    Shader,
    ResourcePack
}

/// <summary>
/// 转移Mod结果状态
/// </summary>
public enum MoveModStatus
{
    Success,
    Updated,
    Copied,
    Incompatible,
    Failed
}

/// <summary>
/// 转移Mod结果
/// </summary>
public partial class MoveModResult : ObservableObject
{
    [ObservableProperty]
    private string _modName = string.Empty;

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _targetPath = string.Empty;

    [ObservableProperty]
    private MoveModStatus _status;

    [ObservableProperty]
    private string _newVersion = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 显示状态文本
    /// </summary>
    public string StatusText
    {
        get
        {
            switch (Status)
            {
                case MoveModStatus.Success:
                    return "VersionManagerPage_ModMovedSuccessText".GetLocalized();
                case MoveModStatus.Updated:
                    return "VersionManagerPage_UpdatedAndMovedText".GetLocalized();
                case MoveModStatus.Copied:
                    return "VersionManagerPage_ModCopiedText".GetLocalized();
                case MoveModStatus.Incompatible:
                    return "VersionManagerPage_ModIncompatibleText".GetLocalized();
                case MoveModStatus.Failed:
                    return "VersionManagerPage_ModMoveFailedText".GetLocalized();
                default:
                    return "VersionManagerPage_UnknownStatusText".GetLocalized();
            }
        }
    }

    /// <summary>
    /// 是否显示为灰字
    /// </summary>
    public bool IsGrayedOut => Status == MoveModStatus.Incompatible || Status == MoveModStatus.Failed;
}

/// <summary>
/// 目标版本信息类，用于转移资源功能
/// </summary>
public partial class TargetVersionInfo : ObservableObject
{
    /// <summary>
    /// 版本名称
    /// </summary>
    [ObservableProperty]
    private string _versionName = string.Empty;

    /// <summary>
    /// 是否兼容
    /// </summary>
    [ObservableProperty]
    private bool _isCompatible;
}
