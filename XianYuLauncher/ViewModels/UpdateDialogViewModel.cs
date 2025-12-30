using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading; using System.Threading.Tasks;
using System.IO;
using XMCL2025.Core.Models;
using XMCL2025.Core.Services;
using System.Diagnostics;

namespace XMCL2025.ViewModels;

/// <summary>
/// 更新弹窗的ViewModel
/// </summary>
public partial class UpdateDialogViewModel : ObservableRecipient
{
    private readonly ILogger<UpdateDialogViewModel> _logger;
    private readonly UpdateService _updateService;
    private CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly DispatcherQueue _dispatcherQueue;
    
    /// <summary>
    /// 更新信息
    /// </summary>
    [ObservableProperty]
    private UpdateInfo _updateInfo;
    
    /// <summary>
    /// 更新标题
    /// </summary>
    [ObservableProperty]
    private string _title;
    
    /// <summary>
    /// 是否显示取消按钮
    /// </summary>
    [ObservableProperty]
    private bool _showCancelButton = true;
    
    /// <summary>
    /// 更新日志文本
    /// </summary>
    [ObservableProperty]
    private string _changelogText;
    
    /// <summary>
    /// 是否正在下载
    /// </summary>
    [ObservableProperty]
    private bool _isDownloading;
    
    /// <summary>
    /// 下载进度
    /// </summary>
    [ObservableProperty]
    private double _downloadProgress;
    
    /// <summary>
    /// 下载状态文本
    /// </summary>
    [ObservableProperty]
    private string _downloadStatusText;
    
    /// <summary>
    /// 下载速度文本
    /// </summary>
    [ObservableProperty]
    private string _downloadSpeedText;
    
    /// <summary>
    /// 预计剩余时间文本
    /// </summary>
    [ObservableProperty]
    private string _estimatedTimeText;
    
    /// <summary>
    /// 取消命令
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        if (IsDownloading)
        {
            // 如果正在下载，取消下载
            _logger.LogInformation("用户取消下载");
            Debug.WriteLine("[DEBUG] 用户取消下载");
            _cts.Cancel();
            IsDownloading = false;
        }
        
        _logger.LogInformation("用户取消更新");
        Debug.WriteLine("[DEBUG] 用户取消更新");
        OnCloseDialog(false);
    }
    
    /// <summary>
    /// 更新命令
    /// </summary>
    [RelayCommand]
    private async Task Update()
    {
        _logger.LogInformation("用户确认更新");
        Debug.WriteLine("[DEBUG] 用户确认更新");
        
        try
        {
            // 开始下载
            IsDownloading = true;
            ShowCancelButton = true; // 下载过程中允许取消
            
            // 确保UI有时间更新，显示下载区域
            await Task.Delay(100);
            
            // 设置临时下载路径
            string tempPath = Path.GetTempPath();
            string downloadPath = Path.Combine(tempPath, $"XianYuLauncher_{UpdateInfo.version}.zip");
            
            // 下载更新包
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
                
                // 确保用户能看到下载完成的状态
                await Task.Delay(1000);
                
                // 暂时预留安装逻辑
                // 这里可以添加解压、证书安装和MSIX安装等步骤
                
                // 安装完成后关闭弹窗
                OnCloseDialog(true);
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
    
    /// <summary>
    /// 下载进度回调
    /// </summary>
    /// <param name="progressInfo">下载进度信息</param>
    private void OnDownloadProgress(DownloadProgressInfo progressInfo)
    {
        // 使用DispatcherQueue将UI更新调度到UI线程
        _dispatcherQueue.TryEnqueue(() =>
        {
            // 更新进度
            DownloadProgress = progressInfo.Progress;
            
            // 更新下载状态文本
            DownloadStatusText = $"正在下载: {progressInfo.Progress:F1}%";
            
            // 更新下载速度
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
            
            // 更新预计剩余时间
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
    
    /// <summary>
    /// 关闭弹窗事件
    /// </summary>
    public event EventHandler<bool> CloseDialog;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="updateService">更新服务</param>
    /// <param name="updateInfo">更新信息</param>
    public UpdateDialogViewModel(ILogger<UpdateDialogViewModel> logger, UpdateService updateService, UpdateInfo updateInfo)
    {
        _logger = logger;
        _updateService = updateService;
        _updateInfo = updateInfo;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        
        // 初始化标题
        Title = string.Format("Version {0} 更新", updateInfo.version);
        
        // 设置是否显示取消按钮
        ShowCancelButton = !updateInfo.important_update;
        
        // 格式化更新日志
        ChangelogText = FormatChangelog(updateInfo.changelog);
        
        // 初始化下载相关属性
        IsDownloading = false;
        DownloadProgress = 0;
        DownloadStatusText = "准备下载...";
        DownloadSpeedText = "速度: 0 B/s";
        EstimatedTimeText = "剩余: 计算中...";
        
        _logger.LogInformation("初始化更新弹窗ViewModel，版本: {Version}, 重要更新: {ImportantUpdate}", updateInfo.version, updateInfo.important_update);
        Debug.WriteLine($"[DEBUG] 初始化更新弹窗ViewModel，版本: {updateInfo.version}, 重要更新: {updateInfo.important_update}");
    }
    
    /// <summary>
    /// 格式化更新日志
    /// </summary>
    /// <param name="changelog">更新日志列表</param>
    /// <returns>格式化后的更新日志文本</returns>
    private string FormatChangelog(List<string> changelog)
    {
        if (changelog == null || !changelog.Any())
        {
            return "暂无更新内容";
        }
        
        return string.Join(Environment.NewLine + "• ", changelog.Prepend(""));
    }
    
    /// <summary>
    /// 触发关闭弹窗事件
    /// </summary>
    /// <param name="result">结果</param>
    protected virtual void OnCloseDialog(bool result)
    {
        _logger.LogInformation("关闭更新弹窗，结果: {Result}", result);
        Debug.WriteLine($"[DEBUG] 关闭更新弹窗，结果: {result}");
        
        // 取消下载
        _cts.Cancel();
        _cts.Dispose();
        
        // 触发关闭弹窗事件
        CloseDialog?.Invoke(this, result);
    }
}