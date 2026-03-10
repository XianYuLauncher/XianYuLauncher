using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher.Features.Protocol;

public sealed class LaunchProtocolCommandHandler : IProtocolCommandHandler
{
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

        if (string.IsNullOrEmpty(targetPath))
        {
            ShowToast("启动错误", "缺少 path 参数，请使用 xianyulauncher://launch/?path=实例路径 格式。");
            EnsureMainWindowInitialized();
            App.MainWindow.Activate();
            return;
        }

        if (ProtocolPathSecurityHelper.IsUncPath(targetPath))
        {
            ShowToast("拦截提示", "为了您的系统安全，已禁止从网络路径(UNC)加载游戏，请使用本地磁盘路径。");
            EnsureMainWindowInitialized();
            App.MainWindow.Activate();
            return;
        }

        if (!System.IO.Directory.Exists(targetPath))
        {
            ShowToast("启动错误", "找不到目标实例路径");
            EnsureMainWindowInitialized();
            App.MainWindow.Activate();
            return;
        }

        var versionName = new System.IO.DirectoryInfo(targetPath).Name;
        if (string.IsNullOrEmpty(versionName))
        {
            return;
        }

        var logger = Serilog.Log.Logger;
        logger.Information("Silent Launch requested for: {VersionName}, Path: {TargetPath}", versionName, targetPath);

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

            var gameLaunchService = App.GetService<IGameLaunchService>();
            var tokenRefreshService = App.GetService<ITokenRefreshService>();
            var profileManager = App.GetService<IProfileManager>();

            var profiles = await profileManager.LoadProfilesAsync();
            var profile = profiles.FirstOrDefault(p => p.IsActive);

            if (profile == null)
            {
                ShowToast("启动失败", "未选择任何账户，请先打开启动器登录。");
                EnsureMainWindowInitialized();
                App.MainWindow.Activate();
                return;
            }

            if (!profile.IsOffline)
            {
                var result = await tokenRefreshService.ValidateAndRefreshTokenAsync(profile);
                if (!result.Success)
                {
                    ShowToast("启动失败", "账户登录已过期，请重新登录。");
                    EnsureMainWindowInitialized();
                    App.MainWindow.Activate();
                    return;
                }
            }

            var launchResult = await gameLaunchService.LaunchGameAsync(
                versionName,
                profile,
                progress => { },
                status => { },
                default,
                null,
                quickPlaySingleplayer,
                quickPlayServer,
                quickPlayPort);

            if (launchResult.GameProcess != null)
            {
                ShowToast("游戏已启动", $"{versionName} 正在运行中...");
                Application.Current.Exit();
            }
            else
            {
                ShowToast("启动失败", "游戏未能启动，请查看日志。");
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
