using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.Launch.ViewModels;
using XianYuLauncher.Features.Shell.Views;

namespace XianYuLauncher.Features.Protocol;

public sealed class LaunchProtocolCommandHandler : IProtocolCommandHandler
{
    private readonly IVersionPathGameLaunchService _versionPathGameLaunchService;

    public LaunchProtocolCommandHandler(IVersionPathGameLaunchService versionPathGameLaunchService)
    {
        _versionPathGameLaunchService = versionPathGameLaunchService;
    }

    public bool CanHandle(ProtocolCommand command) => command is LaunchProtocolCommand;

    public async Task HandleAsync(ProtocolCommand command)
    {
        if (command is not LaunchProtocolCommand launchCommand)
        {
            return;
        }

        var targetPath = launchCommand.TargetPath;
        var mapName = launchCommand.MapName;
        var serverIp = launchCommand.ServerIp;
        var serverPortStr = launchCommand.ServerPort;

        PreparedVersionPathLaunch preparedLaunch;
        try
        {
            preparedLaunch = _versionPathGameLaunchService.PrepareLaunch(targetPath ?? string.Empty);
        }
        catch (InvalidOperationException ex)
        {
            var toastTitle = ex.Message.Contains("网络路径", StringComparison.OrdinalIgnoreCase)
                ? "拦截提示"
                : "启动错误";
            ShowToast(toastTitle, ex.Message);
            EnsureMainWindowInitialized();
            App.MainWindow.Activate();
            return;
        }

        var versionName = preparedLaunch.VersionName;

        var logger = Serilog.Log.Logger;
        logger.Information("Silent Launch requested for: {VersionName}, Path: {TargetPath}", versionName, preparedLaunch.VersionPath);

        try
        {
            var toastTitle = $"正在启动: {versionName}";
            var toastContent = "请稍候，正在准备游戏环境...";

            string? quickPlaySingleplayer = null;
            string? quickPlayServer = null;
            int? quickPlayPort = null;

            if (!string.IsNullOrEmpty(mapName))
            {
                quickPlaySingleplayer = mapName;
                toastTitle = $"正在启动存档: {mapName}";
            }
            else if (!string.IsNullOrEmpty(serverIp))
            {
                toastTitle = $"正在连接服务器: {serverIp}";
                quickPlayServer = serverIp;
                if (int.TryParse(serverPortStr, out var p))
                {
                    quickPlayPort = p;
                }
            }

            ShowToast(toastTitle, toastContent);

            var launchResult = await _versionPathGameLaunchService.LaunchAsync(
                preparedLaunch,
                new VersionPathLaunchOptions
                {
                    ProfileId = launchCommand.ProfileId,
                    QuickPlaySingleplayer = quickPlaySingleplayer,
                    QuickPlayServer = quickPlayServer,
                    QuickPlayPort = quickPlayPort
                });

            if (launchResult.GameProcess != null)
            {
                StartDetachedMonitoring(launchResult.GameProcess, launchResult.LaunchCommand);
                ShowToast("游戏已启动", $"{versionName} 正在运行中...");
                Application.Current.Exit();
            }
            else
            {
                ShowToast("启动失败", launchResult.ErrorMessage ?? "游戏未能启动，请查看日志。");
                EnsureMainWindowInitialized();
                App.MainWindow.Activate();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Silent launch failed");
            ShowToast("启动错误", $"发生异常: {ex.Message}");
            EnsureMainWindowInitialized();
            App.MainWindow.Activate();
        }
    }

    private static void StartDetachedMonitoring(System.Diagnostics.Process gameProcess, string? launchCommand)
    {
        var monitor = App.GetService<IGameProcessMonitor>();
        _ = monitor.MonitorProcessAsync(gameProcess, launchCommand ?? string.Empty).ContinueWith(
            task =>
            {
                if (task.Exception != null)
                {
                    Serilog.Log.Warning(task.Exception.GetBaseException(), "协议静默启动后的进程监控失败");
                }
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private static void EnsureMainWindowInitialized()
    {
        if (App.MainWindow.Content == null)
        {
            var shell = App.GetService<ShellPage>();
            App.MainWindow.Content = shell;

            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(LaunchViewModel).FullName!);
        }
    }

    private static void ShowToast(string title, string content)
    {
        try
        {
            var template = Windows.UI.Notifications.ToastNotificationManager.GetTemplateContent(Windows.UI.Notifications.ToastTemplateType.ToastText02);
            var textNodes = template.GetElementsByTagName("text");
            textNodes[0].InnerText = title;
            textNodes[1].InnerText = content;

            var toast = new Windows.UI.Notifications.ToastNotification(template);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to show toast notification");
        }
    }
}
