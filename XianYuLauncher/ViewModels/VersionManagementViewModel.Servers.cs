using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using fNbt;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.ViewModels;

public partial class VersionManagementViewModel
{
    private List<ServerItem> _allServers = new();

    [ObservableProperty]
    private ObservableCollection<ServerItem> _servers = new();

    [ObservableProperty]
    private bool _isServerListEmpty = true;

    [ObservableProperty]
    private string _serverSearchText = string.Empty;

    /// <summary>
    /// 加载服务器列表
    /// </summary>
    public async Task LoadServersAsync()
    {
        try
        {
            IsLoading = true;
            
            // 获取设置服务和当前状态
            var localSettingsService = App.GetService<XianYuLauncher.Core.Contracts.Services.ILocalSettingsService>();
            
            // 注意：ReadSettingAsync<bool> 如果key不存在可能会返回默认值false，而SettingsViewModel中的默认值是true
            // 因此这里需要使用 bool? 读取并处理默认情况
            var enableVersionIsolationSetting = await localSettingsService.ReadSettingAsync<bool?>("EnableVersionIsolation");
            var enableVersionIsolation = enableVersionIsolationSetting ?? true;
            
            // 捕获和验证路径
            var currentVersionPath = SelectedVersion?.Path;
            var currentMinecraftPath = MinecraftPath;
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] LoadServersAsync - 启动");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 设置: EnableVersionIsolation = {enableVersionIsolation} (Raw: {enableVersionIsolationSetting})");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前版本路径: {currentVersionPath}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前MC路径: {currentMinecraftPath}");

            // 在后台线程处理文件读取和解析
            var servers = await Task.Run(async () =>
            {
                var list = new List<ServerItem>();
                string serversDatPath = string.Empty;
                
                // 根据版本隔离设置决定路径
                if (enableVersionIsolation)
                {
                    if (!string.IsNullOrEmpty(currentVersionPath))
                    {
                        serversDatPath = Path.Combine(currentVersionPath, "servers.dat");
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 版本隔离已启用，使用版本目录下的servers.dat");
                    }
                    else
                    {
                         System.Diagnostics.Debug.WriteLine($"[DEBUG] 版本隔离已启用但版本路径为空，不加载服务器列表");
                    }
                }
                else
                {
                    serversDatPath = Path.Combine(currentMinecraftPath, "servers.dat");
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 版本隔离未启用，使用根目录下的servers.dat");
                }
                
                if (string.IsNullOrEmpty(serversDatPath))
                {
                    return list;
                }
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 目标路径: {serversDatPath}");
                
                if (File.Exists(serversDatPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] servers.dat 文件存在，开始解析");
                    try
                    {
                        var file = new NbtFile();
                        file.LoadFromFile(serversDatPath);

                        var serversTag = file.RootTag.Get<NbtList>("servers");
                        if (serversTag != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] 找到 servers 标签，包含 {serversTag.Count} 个条目");
                            foreach (NbtCompound serverTag in serversTag)
                            {
                                var name = serverTag["name"]?.StringValue ?? "Minecraft Server";
                                var ip = serverTag["ip"]?.StringValue ?? "";
                                var iconBase64 = serverTag["icon"]?.StringValue;
                                var isHidden = serverTag["hidden"]?.ByteValue == 1;

                                if (isHidden)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 服务器 '{name}' 被标记为隐藏，跳过");
                                    continue;
                                }

                                var serverItem = new ServerItem
                                {
                                    Name = name,
                                    Address = ip,
                                    IconBase64 = iconBase64
                                };
                                
                                System.Diagnostics.Debug.WriteLine($"[DEBUG] 读取到服务器: {name} ({ip})");
                                list.Add(serverItem);
                            }
                        }
                        else
                        {
                             System.Diagnostics.Debug.WriteLine($"[DEBUG] servers.dat 中未找到 'servers' 标签");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parsing servers.dat: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] servers.dat 文件不存在");
                }
                return list;
            });

            _allServers = servers;
            FilterServers();

            // 异步加载图标
            foreach (var item in _allServers)
            {
                await item.DecodeIconAsync();
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
            IsLoading = false;
        }
    }

    private void FilterServers()
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() => 
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] FilterServers calling. AllServers count: {_allServers.Count}");
            
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
            System.Diagnostics.Debug.WriteLine($"[DEBUG] FilterServers finished. Visible count: {Servers.Count}");
        });
    }

    [RelayCommand]
    private async Task AddServerAsync()
    {
        // TODO: 实现添加服务器逻辑
        await _dialogService.ShowMessageDialogAsync("功能开发中", "添加服务器功能尚未实现");
    }

    partial void OnServerSearchTextChanged(string value)
    {
        FilterServers();
    }
}
