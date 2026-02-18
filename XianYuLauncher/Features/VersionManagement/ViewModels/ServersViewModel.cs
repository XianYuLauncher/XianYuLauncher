using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Models;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// 服务器管理子 ViewModel —— 拥有服务器 Tab 的全部状态与命令。
/// </summary>
public partial class ServersViewModel : ObservableObject
{
    private readonly IVersionManagementContext _context;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    private List<ServerItem> _allServers = new();

    public ServersViewModel(
        IVersionManagementContext context,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _context = context;
        _navigationService = navigationService;
        _dialogService = dialogService;
    }

    #region 状态属性

    [ObservableProperty]
    private ObservableCollection<ServerItem> _servers = new();

    [ObservableProperty]
    private bool _isServerListEmpty = true;

    [ObservableProperty]
    private string _serverSearchText = string.Empty;

    partial void OnServerSearchTextChanged(string value) => FilterServers();

    #endregion

    #region 数据加载

    /// <summary>
    /// 加载服务器列表
    /// </summary>
    public async Task LoadServersAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return;

        try
        {
            _context.IsLoading = true;

            string serversDatPath = await _context.GetVersionSpecificFilePathAsync("servers.dat");

            var servers = await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(serversDatPath))
                    return new List<ServerItem>();

                return VersionManagementServerDatOps.LoadServers(serversDatPath);
            });

            _allServers = servers;
            FilterServers();

            // 异步加载图标和状态
            foreach (var item in _allServers)
            {
                await item.DecodeIconAsync();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        string host = item.Address;
                        int port = 25565;
                        if (host.Contains(":"))
                        {
                            var parts = host.Split(':');
                            host = parts[0];
                            if (parts.Length > 1 && int.TryParse(parts[1], out int p)) port = p;
                        }

                        var (icon, motd, online, max, ping) = await XianYuLauncher.Core.Helpers.ServerStatusFetcher.PingerAsync(host, port);

                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (ping >= 0)
                                item.UpdateStatus(motd, online, max, ping, icon);
                            else
                            {
                                item.Motd = "无法连接";
                                item.Ping = -1;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error pinging server {item.Name}: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadServersAsync Error: {ex.Message}");
            _allServers.Clear();
            FilterServers();
        }
        finally
        {
            _context.IsLoading = false;
        }
    }

    public void FilterServers()
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (string.IsNullOrWhiteSpace(ServerSearchText))
            {
                Servers = new ObservableCollection<ServerItem>(_allServers);
            }
            else
            {
                var filtered = _allServers.Where(x =>
                    x.Name.Contains(ServerSearchText, StringComparison.OrdinalIgnoreCase) ||
                    x.Address.Contains(ServerSearchText, StringComparison.OrdinalIgnoreCase));
                Servers = new ObservableCollection<ServerItem>(filtered);
            }

            IsServerListEmpty = Servers.Count == 0;
        });
    }

    #endregion

    #region 命令

    [RelayCommand]
    private async Task AddServerAsync()
    {
        var stackPanel = new StackPanel { Spacing = 12 };

        var nameInput = new TextBox
        {
            Header = "服务器名称",
            PlaceholderText = "Minecraft Server",
            Text = "Minecraft Server"
        };
        var addrInput = new TextBox
        {
            Header = "服务器地址",
            PlaceholderText = "例如: 127.0.0.1"
        };

        stackPanel.Children.Add(nameInput);
        stackPanel.Children.Add(addrInput);

        var dialog = new ContentDialog
        {
            Title = "添加服务器",
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = stackPanel,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await _dialogService.ShowDialogAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            string name = nameInput.Text.Trim();
            string addr = addrInput.Text.Trim();

            if (string.IsNullOrEmpty(name)) name = "Minecraft Server";
            if (string.IsNullOrEmpty(addr)) return;

            await AddServerToNbtAsync(name, addr);
        }
    }

    private async Task AddServerToNbtAsync(string name, string address)
    {
        try
        {
            string serversDatPath = await _context.GetVersionSpecificFilePathAsync("servers.dat");
            if (string.IsNullOrEmpty(serversDatPath)) return;

            await Task.Run(() => VersionManagementServerDatOps.AddServer(serversDatPath, name, address));
            await LoadServersAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding server: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LaunchServer(ServerItem server)
    {
        if (server == null || _context.SelectedVersion == null) return;

        string address = server.Address;
        int? port = null;

        if (!string.IsNullOrEmpty(address) && address.Contains(':'))
        {
            var parts = address.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int p))
            {
                address = parts[0];
                port = p;
            }
        }

        var param = new LaunchMapParameter
        {
            VersionId = _context.SelectedVersion.Name,
            ServerAddress = address,
            ServerPort = port
        };

        _navigationService.NavigateTo(typeof(LaunchViewModel).FullName, param);
    }

    [RelayCommand]
    private async Task DeleteServerAsync(ServerItem server)
    {
        if (server == null) return;

        var confirmed = await _dialogService.ShowConfirmationDialogAsync(
            "删除服务器",
            $"确定要删除服务器 '{server.Name}' 吗?",
            "删除", "取消");
        if (!confirmed) return;

        try
        {
            string serversDatPath = await _context.GetVersionSpecificFilePathAsync("servers.dat");
            bool removed = VersionManagementServerDatOps.RemoveServer(
                serversDatPath,
                server.Name ?? string.Empty,
                server.Address ?? string.Empty);

            if (removed)
            {
                await LoadServersAsync();
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("错误", $"删除服务器失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CreateServerShortcutAsync(ServerItem server)
    {
        if (server == null || _context.SelectedVersion == null) return;

        try
        {
            _context.StatusMessage = $"正在创建快捷方式: {server.Name}...";

            var shortcutPath = VersionManagementShortcutOps.BuildServerShortcutPath(server.Name, _context.SelectedVersion.Name);
            var shortcutName = Path.GetFileNameWithoutExtension(shortcutPath);

            if (Helpers.ShortcutHelper.ShortcutExists(shortcutPath))
            {
                var overwrite = await _dialogService.ShowConfirmationDialogAsync(
                    "快捷方式已存在",
                    $"桌面上已存在 {shortcutName} 的快捷方式。\n是否覆盖现有快捷方式？",
                    "覆盖", "取消");
                if (!overwrite) return;
            }

            shortcutName = await VersionManagementShortcutOps.CreateServerShortcutFileAsync(
                server,
                _context.SelectedVersion.Name,
                _context.SelectedVersion.Path);

            _context.StatusMessage = $"快捷方式已创建: {shortcutName}";

            await _dialogService.ShowMessageDialogAsync("快捷方式已创建",
                $"已在桌面创建 {shortcutName} 的快捷方式。\n双击可直接连接此服务器。");
        }
        catch (Exception ex)
        {
            _context.StatusMessage = "创建快捷方式失败";
            System.Diagnostics.Debug.WriteLine($"创建服务器快捷方式失败: {ex}");
            await _dialogService.ShowMessageDialogAsync("创建失败", "创建快捷方式失败，请检查桌面权限或稍后重试。");
        }
    }

    #endregion
}
