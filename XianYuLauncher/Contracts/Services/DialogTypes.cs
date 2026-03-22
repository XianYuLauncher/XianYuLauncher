using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Contracts.Services;

public sealed class SettingsCustomSourceDialogRequest
{
    public string Title { get; init; } = string.Empty;

    public string PrimaryButtonText { get; init; } = "保存";

    public string CloseButtonText { get; init; } = "取消";

    public string Name { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public DownloadSourceTemplateType Template { get; init; } = DownloadSourceTemplateType.Official;

    public int Priority { get; init; } = 100;

    public bool Enabled { get; init; } = true;

    public bool ShowEnabledSwitch { get; init; } = true;

    public bool ShowTemplateSelection { get; init; } = true;
}

public sealed class SettingsCustomSourceDialogResult
{
    public string Name { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public DownloadSourceTemplateType Template { get; init; }

    public int Priority { get; init; }

    public bool Enabled { get; init; }
}

public sealed class AddServerDialogResult
{
    public string Name { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;
}

public enum LoginMethodSelectionResult
{
    Cancel = 0,
    Browser = 1,
    DeviceCode = 2,
}

public sealed class PublisherDialogItem
{
    public string Name { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string AvatarUrl { get; init; } = string.Empty;
}

public enum CrashReportDialogAction
{
    Close = 0,
    ExportLogs = 1,
    ViewDetails = 2,
}

public enum SkinModelSelectionResult
{
    Cancel = 0,
    Steve = 1,
    Alex = 2,
}