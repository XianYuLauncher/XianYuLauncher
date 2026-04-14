using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Features.Dialogs.ViewModels;

/// <summary>
/// 更新弹窗的ViewModel
/// </summary>
public partial class UpdateDialogViewModel : ObservableRecipient
{
    private readonly ILogger<UpdateDialogViewModel> _logger;
    private readonly UpdateService _updateService;
    private CancellationTokenSource _cts = new();
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    private UpdateInfo _updateInfo;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _showCancelButton = true;

    [ObservableProperty]
    private string _changelogText;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatusText;

    [ObservableProperty]
    private string _downloadSpeedText;

    [ObservableProperty]
    private string _estimatedTimeText;

    [RelayCommand]
    private void Cancel()
    {
        if (IsDownloading)
        {
            _logger.LogInformation("用户取消下载");
            Debug.WriteLine("[DEBUG] 用户取消下载");
            _cts.Cancel();
            IsDownloading = false;
        }

        _logger.LogInformation("用户取消更新");
        Debug.WriteLine("[DEBUG] 用户取消更新");
        OnCloseDialog(false);
    }

    [RelayCommand]
    private async Task Update()
    {
        _logger.LogInformation("用户确认更新");
        Debug.WriteLine("[DEBUG] 用户确认更新");

        try
        {
            IsDownloading = true;
            ShowCancelButton = !UpdateInfo.important_update;
            await Task.Delay(100);

            string tempPath = Path.GetTempPath();
            string downloadPath = Path.Combine(tempPath, $"XianYuLauncher_{UpdateInfo.version}.zip");

            bool success = await _updateService.DownloadUpdatePackageAsync(
                UpdateInfo,
                downloadPath,
                OnDownloadProgress,
                _cts.Token);

            if (success)
            {
                _logger.LogInformation("更新包下载成功");
                Debug.WriteLine("[DEBUG] 更新包下载成功");
                DownloadStatusText = "下载完成，准备安装...";

                await Task.Delay(1000);

                try
                {
                    _logger.LogInformation("开始解压更新包: {DownloadPath}", downloadPath);
                    Debug.WriteLine($"[DEBUG] 开始解压更新包: {downloadPath}");
                    DownloadStatusText = "正在解压更新包...";

                    var extractResult = await _updateService.ExtractUpdatePackageAsync(downloadPath);

                    if (!string.IsNullOrEmpty(extractResult.CertificateFilePath))
                    {
                        _logger.LogInformation("检查证书安装状态: {CertificateFilePath}", extractResult.CertificateFilePath);
                        Debug.WriteLine($"[DEBUG] 检查证书安装状态: {extractResult.CertificateFilePath}");
                        DownloadStatusText = "正在检查证书...";

                        bool isCertificateInstalled = _updateService.IsCertificateInstalled(extractResult.CertificateFilePath);

                        if (!isCertificateInstalled)
                        {
                            _logger.LogInformation("证书未安装，尝试静默安装");
                            Debug.WriteLine($"[DEBUG] 证书未安装，尝试静默安装: {extractResult.CertificateFilePath}");
                            DownloadStatusText = "正在尝试自动安装证书...";

                            await _updateService.InstallCertificateSilentlyAsync(extractResult.CertificateFilePath);
                            isCertificateInstalled = _updateService.IsCertificateInstalled(extractResult.CertificateFilePath);
                        }

                        if (!isCertificateInstalled)
                        {
                            _logger.LogInformation("证书未安装，打开证书属性页");
                            Debug.WriteLine("[DEBUG] 证书未安装，打开证书属性页");

                            _updateService.OpenCertificateProperties(extractResult.CertificateFilePath);
                            DownloadStatusText = "请安装证书后继续: 右键证书->安装->本地计算机->受信任的根证书颁发机构";

                            _logger.LogInformation("请右键证书->安装->本地计算机->受信任的根证书颁发机构");
                            Debug.WriteLine("[DEBUG] 请右键证书->安装->本地计算机->受信任的根证书颁发机构");

                            int waitSeconds = 0;
                            const int MaxWaitSeconds = 999;

                            while (!_updateService.IsCertificateInstalled(extractResult.CertificateFilePath) && waitSeconds < MaxWaitSeconds)
                            {
                                _logger.LogInformation("等待用户安装证书，已等待 {WaitSeconds} 秒", waitSeconds);
                                Debug.WriteLine($"[DEBUG] 等待用户安装证书，已等待 {waitSeconds} 秒");

                                await Task.Delay(5000);
                                waitSeconds += 5;
                            }

                            if (waitSeconds >= MaxWaitSeconds)
                            {
                                _logger.LogWarning("证书安装超时");
                                Debug.WriteLine("[DEBUG] 证书安装超时");
                                DownloadStatusText = "证书安装超时，请手动安装证书后重试";
                                IsDownloading = false;
                                return;
                            }

                            _logger.LogInformation("证书安装成功");
                            Debug.WriteLine("[DEBUG] 证书安装成功");
                            DownloadStatusText = "证书安装成功，准备安装MSIX包...";
                        }
                    }

                    _logger.LogInformation("开始安装MSIX包: {MsixFilePath}", extractResult.MsixFilePath);
                    Debug.WriteLine($"[DEBUG] 开始安装MSIX包: {extractResult.MsixFilePath}");
                    DownloadStatusText = "正在请求系统更新，应用即将自动关闭...";
                    await Task.Delay(500);

                    bool installSuccess = await _updateService.InstallMsixPackageAsync(extractResult.ExtractDirectory, extractResult.MsixFilePath);

                    if (installSuccess)
                    {
                        _logger.LogInformation("MSIX包安装请求成功");
                        Debug.WriteLine("[DEBUG] MSIX包安装请求成功");
                        DownloadStatusText = "更新请求已提交";

                        await Task.Delay(1000);
                        OnCloseDialog(true);
                    }
                    else
                    {
                        _logger.LogError("MSIX包安装失败");
                        Debug.WriteLine("[DEBUG] MSIX包安装失败");
                        DownloadStatusText = "安装请求失败，请尝试手动下载安装包";
                        IsDownloading = false;
                        _updateService.CleanupTempFiles(extractResult.ExtractDirectory);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "安装过程中发生错误");
                    Debug.WriteLine($"[DEBUG] 安装过程中发生错误: {ex.Message}");
                    DownloadStatusText = $"安装失败: {ex.Message}";
                    IsDownloading = false;
                }
            }
            else
            {
                _logger.LogError("更新包下载失败");
                Debug.WriteLine("[DEBUG] 更新包下载失败");
                DownloadStatusText = "下载失败，请重试";
                IsDownloading = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新过程中发生错误");
            Debug.WriteLine($"[DEBUG] 更新过程中发生错误: {ex.Message}");
            DownloadStatusText = $"下载失败: {ex.Message}";
            IsDownloading = false;
        }
    }

    public event EventHandler<bool>? CloseDialog;

    public UpdateDialogViewModel(ILogger<UpdateDialogViewModel> logger, UpdateService updateService, UpdateInfo updateInfo)
    {
        _logger = logger;
        _updateService = updateService;
        _updateInfo = updateInfo;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        Title = string.Format("Version {0} 更新", updateInfo.version);
        ShowCancelButton = !updateInfo.important_update;
        ChangelogText = FormatChangelog(updateInfo.changelog);
        IsDownloading = false;
        DownloadProgress = 0;
        DownloadStatusText = "准备下载...";
        DownloadSpeedText = "速度: 0 B/s";
        EstimatedTimeText = "剩余: 计算中...";

        _logger.LogInformation("初始化更新弹窗ViewModel，版本: {Version}, 重要更新: {ImportantUpdate}", updateInfo.version, updateInfo.important_update);
        Debug.WriteLine($"[DEBUG] 初始化更新弹窗ViewModel，版本: {updateInfo.version}, 重要更新: {updateInfo.important_update}");
    }

    private void OnDownloadProgress(DownloadProgressInfo progressInfo)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            DownloadProgress = progressInfo.Progress;
            DownloadStatusText = $"正在下载: {progressInfo.Progress:F1}%";

            string speedUnit = "B/s";
            double speed = progressInfo.SpeedBytesPerSecond;

            if (speed >= 1024 * 1024)
            {
                speed /= 1024 * 1024;
                speedUnit = "MB/s";
            }
            else if (speed >= 1024)
            {
                speed /= 1024;
                speedUnit = "KB/s";
            }

            DownloadSpeedText = $"速度: {speed:F1} {speedUnit}";

            if (progressInfo.EstimatedTimeRemaining.TotalSeconds > 0)
            {
                string timeText;
                if (progressInfo.EstimatedTimeRemaining.TotalHours >= 1)
                {
                    timeText = $"{progressInfo.EstimatedTimeRemaining.TotalHours:F1} 小时";
                }
                else if (progressInfo.EstimatedTimeRemaining.TotalMinutes >= 1)
                {
                    timeText = $"{progressInfo.EstimatedTimeRemaining.TotalMinutes:F1} 分钟";
                }
                else
                {
                    timeText = $"{progressInfo.EstimatedTimeRemaining.TotalSeconds:F0} 秒";
                }

                EstimatedTimeText = $"剩余: {timeText}";
            }
            else
            {
                EstimatedTimeText = "剩余: 计算中...";
            }
        });
    }

    private string FormatChangelog(List<string> changelog)
    {
        if (changelog == null || !changelog.Any())
        {
            return "暂无更新内容";
        }

        return string.Join(Environment.NewLine + "• ", changelog.Prepend(""));
    }

    protected virtual void OnCloseDialog(bool result)
    {
        _logger.LogInformation("关闭更新弹窗，结果: {Result}", result);
        Debug.WriteLine($"[DEBUG] 关闭更新弹窗，结果: {result}");

        _cts.Cancel();
        _cts.Dispose();
        CloseDialog?.Invoke(this, result);
    }
}