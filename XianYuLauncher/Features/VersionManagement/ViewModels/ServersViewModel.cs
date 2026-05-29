using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.Launch.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// 服务器管理子 ViewModel —— 拥有服务器 Tab 的全部状态与命令。
/// </summary>
public partial class ServersViewModel : ObservableObject
{
    private readonly IVersionManagementContext _context;
    private readonly INavigationService _navigationService;
    private readonly ICommonDialogService _dialogService;
    private readonly IAccountDialogService _profileDialogService;
    private readonly IAccountManager _accountManager;
    private readonly ISelectionDialogService _selectionDialogService;
    private readonly IUiDispatcher _uiDispatcher;

    private List<ServerItem> _allServers = new();

    public ServersViewModel(
        IVersionManagementContext context,
        INavigationService navigationService,
        ICommonDialogService dialogService,
        IAccountDialogService profileDialogService,
        IAccountManager accountManager,
        ISelectionDialogService selectionDialogService,
        IUiDispatcher uiDispatcher)
    {
        _context = context;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _profileDialogService = profileDialogService;
        _accountManager = accountManager;
        _selectionDialogService = selectionDialogService;
        _uiDispatcher = uiDispatcher;
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
                await item.DecodeIconAsync(_uiDispatcher);

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

                        _uiDispatcher.TryEnqueue(() =>
                        {
                            if (ping >= 0)
                                item.UpdateStatus(motd, online, max, ping, icon, _uiDispatcher);
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
        _uiDispatcher.TryEnqueue(() =>
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
        var input = await _selectionDialogService.ShowAddServerDialogAsync();
        if (input == null) return;

        var name = input.Name;
        var addr = input.Address;

        if (string.IsNullOrEmpty(name)) name = "Minecraft Server";
        if (string.IsNullOrEmpty(addr)) return;

        await AddServerToNbtAsync(name, addr);
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

        _navigationService.NavigateTo(typeof(LaunchViewModel).FullName!, param);
    }

    [RelayCommand]
    private async Task DeleteServerAsync(ServerItem server)
    {
        if (server == null) return;

        var confirmed = await _dialogService.ShowConfirmationDialogAsync(
            "Dialog_Server_DeleteConfirm_Title".GetLocalized(),
            "Dialog_Server_DeleteConfirm_Content_Format".GetLocalized(server.Name ?? string.Empty),
            "Dialog_Delete".GetLocalized(),
            "Dialog_Cancel".GetLocalized());
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
            await _dialogService.ShowMessageDialogAsync(
                "Msg_Error".GetLocalized(),
                "Dialog_Server_DeleteFailed_Format".GetLocalized(ex.Message));
        }
    }

    [RelayCommand]
    private async Task CreateServerShortcutAsync(ServerItem server)
    {
        if (server == null || _context.SelectedVersion == null) return;

        try
        {
            var profiles = await _accountManager.LoadAccountsAsync();
            MinecraftAccount? selectedProfile = null;
            if (profiles.Count > 0)
            {
                selectedProfile = await _profileDialogService.ShowLauncherAccountSelectionDialogAsync(
                    profiles,
                    "LauncherAccountDialog_ShortcutTitle".GetLocalized(),
                    "LauncherAccountDialog_ShortcutPrimaryButton".GetLocalized(),
                    "LauncherAccountDialog_CloseButton".GetLocalized());

                if (selectedProfile == null)
                {
                    return;
                }
            }

            _context.StatusMessage = $"正在创建快捷方式: {server.Name}...";

            var shortcutPath = VersionManagementShortcutOps.BuildServerShortcutPath(server.Name, _context.SelectedVersion.Name, selectedProfile?.Name);
            var shortcutName = Path.GetFileNameWithoutExtension(shortcutPath);

            if (Helpers.ShortcutHelper.ShortcutExists(shortcutPath))
            {
                var overwrite = await _dialogService.ShowConfirmationDialogAsync(
                    "Dialog_Shortcut_Overwrite_Title".GetLocalized(),
                    "Dialog_Shortcut_Overwrite_Content_Format".GetLocalized(shortcutName),
                    "Dialog_Overwrite".GetLocalized(),
                    "Dialog_Cancel".GetLocalized());
                if (!overwrite) return;
            }

            shortcutName = await VersionManagementShortcutOps.CreateServerShortcutFileAsync(
                server,
                _context.SelectedVersion.Name,
                _context.SelectedVersion.Path,
                selectedProfile);

            _context.StatusMessage = $"快捷方式已创建: {shortcutName}";

            await _dialogService.ShowMessageDialogAsync(
                "Dialog_Shortcut_Created_Title".GetLocalized(),
                "Dialog_Shortcut_Created_Server_Content_Format".GetLocalized(shortcutName));
        }
        catch (Exception ex)
        {
            _context.StatusMessage = "创建快捷方式失败";
            System.Diagnostics.Debug.WriteLine($"创建服务器快捷方式失败: {ex}");
            await _dialogService.ShowMessageDialogAsync(
                "Dialog_Shortcut_CreateFailed_Title".GetLocalized(),
                "Dialog_Shortcut_CreateFailed_Server_Content".GetLocalized());
        }
    }

    #endregion
}
