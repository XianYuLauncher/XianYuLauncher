using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Services;

public class DialogService : IDialogService
{
    private XamlRoot _xamlRoot;
    // 使用信号量确保同一时间只有一个弹窗显示，防止 WinUI 崩溃 (COM 0x80000019)
    private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);

    public void SetXamlRoot(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
    }
    
    /// <summary>
    /// 安全显示弹窗，自动处理队列
    /// </summary>
    private async Task<ContentDialogResult> ShowSafeAsync(ContentDialog dialog)
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (dialog.XamlRoot == null)
            {
                var root = GetXamlRoot();
                if (root == null) return ContentDialogResult.None;
                dialog.XamlRoot = root;
            }
            
            return await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DialogService] 弹窗显示异常: {ex.Message}");
            return ContentDialogResult.None;
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    private XamlRoot GetXamlRoot()
    {
        // 如果显式设置了 XamlRoot，优先使用
        if (_xamlRoot != null)
        {
            return _xamlRoot;
        }

        // 尝试从主窗口获取 (对于 WinUI 3 来说，ContentDialog 必须设置 XamlRoot)
        if (App.MainWindow?.Content?.XamlRoot is XamlRoot root)
        {
            return root;
        }

        return null;
    }

    public async Task ShowMessageDialogAsync(string title, string message, string closeButtonText = "确定")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close
        };

        await ShowSafeAsync(dialog);
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message, string primaryButtonText = "是", string closeButtonText = "否")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await ShowSafeAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowJavaNotFoundDialogAsync(string requiredVersion, Action onManualDownload, Action onAutoDownload)
    {
        var dialog = new ContentDialog
        {
            Title = "Java运行时环境未找到",
            Content = $"未找到适用于当前游戏版本的Java运行时环境。\n\n游戏版本需要: Java {requiredVersion}\n\n推荐使用自动下载功能，启动器将自动安装并配置环境。",
            PrimaryButtonText = "自动下载(推荐)",
            SecondaryButtonText = "手动下载",
            CloseButtonText = "取消"
        };

        dialog.PrimaryButtonClick += (s, e) => onAutoDownload?.Invoke();
        dialog.SecondaryButtonClick += (s, e) => onManualDownload?.Invoke();

        await ShowSafeAsync(dialog);
    }

    public async Task ShowOfflineLaunchTipDialogAsync(int offlineLaunchCount, Action onSupportAction)
    {
        var dialog = new ContentDialog
        {
            Title = "离线游玩提示",
            Content = $"您已经使用离线模式启动{offlineLaunchCount}次了,支持一下正版吧！",
            PrimaryButtonText = "知道了",
            SecondaryButtonText = "支持正版"
        };
        
        var result = await ShowSafeAsync(dialog);
        if (result == ContentDialogResult.Secondary)
        {
            onSupportAction?.Invoke();
        }
    }

    public async Task<bool> ShowTokenExpiredDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "LaunchPage_TokenExpiredTitle".GetLocalized(),
            Content = "LaunchPage_TokenExpiredContent".GetLocalized(),
            PrimaryButtonText = "LaunchPage_GoToLoginText".GetLocalized(),
            CloseButtonText = "TutorialPage_CancelButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary
        };
        
        var result = await ShowSafeAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowExportSuccessDialogAsync(string filePath)
    {
        var dialog = new ContentDialog
        {
            Title = "导出成功",
            Content = $"启动参数已成功导出到桌面:\n{System.IO.Path.GetFileName(filePath)}\n\n您可以双击该文件来启动游戏。",
            PrimaryButtonText = "打开文件位置",
            CloseButtonText = "确定"
        };
        
        dialog.PrimaryButtonClick += (s, e) =>
        {
            // 打开文件所在文件夹并选中文件
            try 
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch {}
        };
        
        await ShowSafeAsync(dialog);
    }

    public async Task<bool> ShowRegionRestrictedDialogAsync(string errorMessage)
    {
        var dialog = new ContentDialog
        {
            Title = "地区限制",
            Content = errorMessage,
            PrimaryButtonText = "前往",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await ShowSafeAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        return await ShowSafeAsync(dialog);
    }

    public async Task ShowProgressDialogAsync(string title, string message, Func<IProgress<double>, IProgress<string>, CancellationToken, Task> workCallback)
    {
        var progressBar = new ProgressBar { Maximum = 100, Value = 0, MinHeight = 4, Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 10), IsIndeterminate = true };
        var statusText = new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap };
        
        var contentPanel = new StackPanel();
        contentPanel.Children.Add(statusText);
        contentPanel.Children.Add(progressBar);
        
        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentPanel,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.None
        };

        var cts = new CancellationTokenSource();
        
        dialog.CloseButtonClick += (s, e) => 
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
        };

        var progress = new Progress<double>(p => 
        {
            progressBar.IsIndeterminate = false;
            progressBar.Value = p;
        });
        
        IProgress<string> statusProgress = new Progress<string>(s => statusText.Text = s);

        dialog.Opened += (s, e) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await workCallback(progress, statusProgress, cts.Token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    statusProgress.Report($"操作失败: {ex.Message}");
                    await Task.Delay(2000);
                }
                finally
                {
                    // 确保在 UI 线程上关闭
                    dialog.DispatcherQueue.TryEnqueue(() => 
                    {
                        try { dialog.Hide(); } catch { }
                    });
                }
            });
        };

        await ShowSafeAsync(dialog);
    }
}
