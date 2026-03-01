using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Features.VersionManagement.Services;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

public partial class ResourceTransferStateViewModel : ObservableObject
{
    private readonly IResourceTransferInfrastructureService _resourceTransferInfrastructureService;

    public ResourceTransferStateViewModel(IResourceTransferInfrastructureService resourceTransferInfrastructureService)
    {
        _resourceTransferInfrastructureService = resourceTransferInfrastructureService;
    }

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string _currentDownloadItem = string.Empty;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadProgressDialogTitle = "VersionManagerPage_UpdatingModsText".GetLocalized();

    [ObservableProperty]
    private ResourceMoveType _currentResourceMoveType;

    [ObservableProperty]
    private bool _isMoveResourcesDialogVisible;

    [ObservableProperty]
    private List<MoveModResult> _moveResults = new();

    [ObservableProperty]
    private bool _isMoveResultDialogVisible;

    [ObservableProperty]
    private ObservableCollection<TargetVersionInfo> _targetVersions = new();

    [ObservableProperty]
    private TargetVersionInfo? _selectedTargetVersion;

    public async Task LoadTargetVersionsAsync()
    {
        TargetVersions.Clear();

        var installedVersions = await _resourceTransferInfrastructureService.GetInstalledVersionsAsync();
        foreach (var installedVersion in installedVersions)
        {
            TargetVersions.Add(new TargetVersionInfo
            {
                VersionName = installedVersion,
                IsCompatible = true
            });
        }
    }

    public async Task<bool> DownloadModAsync(string downloadUrl, string destinationPath, CancellationToken cancellationToken)
    {
        try
        {
            var modName = Path.GetFileName(destinationPath);
            System.Diagnostics.Debug.WriteLine($"开始下载Mod: {downloadUrl} 到 {destinationPath}");

            CurrentDownloadItem = modName;

            var progress = new Progress<double>(value =>
            {
                DownloadProgress = value;
            });

            var success = await _resourceTransferInfrastructureService.DownloadFileWithProgressAsync(
                downloadUrl,
                destinationPath,
                progress,
                cancellationToken);

            if (success)
            {
                System.Diagnostics.Debug.WriteLine($"Mod下载完成: {destinationPath}");
                return true;
            }

            System.Diagnostics.Debug.WriteLine("下载Mod失败: 服务返回失败");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"下载Mod失败: {ex.Message}");
            return false;
        }
    }
}
