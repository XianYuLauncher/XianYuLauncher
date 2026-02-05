using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using fNbt;
using XianYuLauncher.Models;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers; // Ensure Core.Helpers is included for AppEnvironment

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

    [RelayCommand]
    private async Task AddServerAsync()
    {
        // 1. 构建对话框内容
        // 避免直接在XAML定义Popup，使用代码构建简单的 StackPanel + TextBoxes
        var stackPanel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 12 };
        
        var nameInput = new Microsoft.UI.Xaml.Controls.TextBox 
        { 
            Header = "服务器名称", 
            PlaceholderText = "Minecraft Server",
            Text = "Minecraft Server" 
        };
        var addrInput = new Microsoft.UI.Xaml.Controls.TextBox 
        { 
            Header = "服务器地址", 
            PlaceholderText = "例如: 127.0.0.1" 
        };

        stackPanel.Children.Add(nameInput);
        stackPanel.Children.Add(addrInput);

        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style,
            Title = "添加服务器",
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
            Content = stackPanel
        };

        // 2. 显示并等待结果
        var result = await dialog.ShowAsync();

        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            string name = nameInput.Text.Trim();
            string addr = addrInput.Text.Trim();

            if (string.IsNullOrEmpty(name)) name = "Minecraft Server";
            if (string.IsNullOrEmpty(addr)) return; // 地址必填

            await AddServerToNbtAsync(name, addr);
        }
    }

    private async Task AddServerToNbtAsync(string name, string address)
    {
        try
        {
            // 使用统一的路径获取方法
            string serversDatPath = await GetVersionSpecificFilePathAsync("servers.dat");
            
            if (string.IsNullOrEmpty(serversDatPath)) return;

            // 确保目录存在
            var dir = Path.GetDirectoryName(serversDatPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            await Task.Run(() => 
            {
                var file = new NbtFile();
                if (File.Exists(serversDatPath))
                {
                    try { file.LoadFromFile(serversDatPath); } catch {}
                }

                // 确保根结构完整
                if (file.RootTag == null) file.RootTag = new NbtCompound("");
                
                var serversList = file.RootTag.Get<NbtList>("servers");
                if (serversList == null)
                {
                    serversList = new NbtList("servers", NbtTagType.Compound);
                    file.RootTag.Add(serversList);
                }

                // 创建新条目
                var newServer = new NbtCompound();
                newServer.Add(new NbtString("name", name));
                newServer.Add(new NbtString("ip", address));
                newServer.Add(new NbtByte("hidden", 0));
                
                // 添加到列表末尾
                serversList.Add(newServer);

                file.SaveToFile(serversDatPath, NbtCompression.None);
            });

            // 刷新列表
            await LoadServersAsync();
        }
        catch (Exception ex)
        {
            // 简单错误处理，实际可能需要弹窗提示
            System.Diagnostics.Debug.WriteLine($"Error adding server: {ex.Message}");
        }
    }

    /// <summary>
    /// 加载服务器列表
    /// </summary>
    public async Task LoadServersAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return;

        try
        {
            IsLoading = true;
            
            // 获取设置服务和当前状态
            var localSettingsService = App.GetService<ILocalSettingsService>();
            
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

            // 使用统一的路径获取方法
            string serversDatPath = await GetVersionSpecificFilePathAsync("servers.dat");

            // 在后台线程处理文件读取和解析
            var servers = await Task.Run(async () =>
            {
                var list = new List<ServerItem>();
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 目标路径: {serversDatPath}");
                
                if (string.IsNullOrEmpty(serversDatPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 服务器文件路径为空，不加载服务器列表");
                    return list;
                }
                
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

            // 异步加载图标和状态
            foreach (var item in _allServers)
            {
                await item.DecodeIconAsync();
                
                // 在后台刷新服务器状态
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
                            {
                                item.UpdateStatus(motd, online, max, ping, icon);
                            }
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

    partial void OnServerSearchTextChanged(string value)
    {
        FilterServers();
    }

    [RelayCommand]
    private void LaunchServer(ServerItem server)
    {
        if (server == null)
        {
             return;
        }

        if (SelectedVersion == null)
        {
             App.MainWindow.DispatcherQueue.TryEnqueue(async () => 
             {
                await new ContentDialog
                {
                    Title = "提示",
                    Content = "请先在版本列表中选择一个版本",
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style
                }.ShowAsync();
             });
             return;
        }

        string address = server.Address;
        int? port = null;

        // 解析地址和端口
        if (!string.IsNullOrEmpty(address) && address.Contains(':'))
        {
            var parts = address.Split(':');
            // 简单处理 IPv4:Port 情况，暂不处理 IPv6 [::]:Port 情况
            if (parts.Length == 2 && int.TryParse(parts[1], out int p))
            {
                address = parts[0];
                port = p;
            }
        }

        var param = new LaunchMapParameter
        {
            VersionId = SelectedVersion.Name,
            ServerAddress = address,
            ServerPort = port
        };

        _navigationService.NavigateTo(typeof(LaunchViewModel).FullName, param);
    }

    [RelayCommand]
    private async Task DeleteServerAsync(ServerItem server)
    {
        if (server == null) return;

        var confirmDialog = new ContentDialog
        {
            Title = "删除服务器",
            Content = $"确定要删除服务器 '{server.Name}' 吗?",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style
        };

        var result = await confirmDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        
        try
        {
            // 使用统一的路径获取方法
            string serversDatPath = await GetVersionSpecificFilePathAsync("servers.dat");

            System.Diagnostics.Debug.WriteLine($"[DeleteServer] Target file: {serversDatPath}");

            if (!File.Exists(serversDatPath)) 
            {
                 System.Diagnostics.Debug.WriteLine($"[DeleteServer] File not found.");
                 return;
            }
            
            var file = new NbtFile();
            file.LoadFromFile(serversDatPath);
            var root = file.RootTag;
            var list = root.Get<NbtList>("servers");
            
            if (list == null) return;
            
            NbtCompound toRemove = null;
            // Simple matching by name and ip
            foreach (NbtCompound tag in list)
            {
                var name = tag["name"]?.StringValue ?? "";
                var ip = tag["ip"]?.StringValue ?? "";
                
                // 增加空值保护
                if (server.Name == null) server.Name = "";
                if (server.Address == null) server.Address = "";

                if (name == server.Name && ip == server.Address)
                {
                    toRemove = tag;
                    break;
                }
            }
            
            if (toRemove != null)
            {
                System.Diagnostics.Debug.WriteLine($"[DeleteServer] Removing server: {server.Name}");
                list.Remove(toRemove);
                file.SaveToFile(serversDatPath, NbtCompression.None);
                
                App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    await LoadServersAsync();
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DeleteServer] Server not found in NBT list.");
            }
        }
        catch (Exception ex)
        {
            await new ContentDialog
            {
                Title = "错误",
                Content = $"删除服务器失败: {ex.Message}",
                CloseButtonText = "确定",
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style
            }.ShowAsync();
        }
    }

    /// <summary>
    /// 创建服务器快捷方式命令
    /// </summary>
    [RelayCommand]
    private async Task CreateServerShortcutAsync(ServerItem server)
    {
        if (server == null || SelectedVersion == null) return;
        
        try
        {
            StatusMessage = $"正在创建快捷方式: {server.Name}...";
            
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string safeServerName = string.Join("_", server.Name.Split(Path.GetInvalidFileNameChars()));
            string safeVersionName = string.Join("_", SelectedVersion.Name.Split(Path.GetInvalidFileNameChars()));
            string shortcutName = $"{safeVersionName} - {safeServerName}";
            string shortcutPath = Path.Combine(desktopPath, $"{shortcutName}.url");
            
            // Check if shortcut already exists
            if (Helpers.ShortcutHelper.ShortcutExists(shortcutPath))
            {
                try
                {
                    var dialogService = App.GetService<IDialogService>();
                    if (dialogService != null)
                    {
                        var result = await dialogService.ShowConfirmationDialogAsync("快捷方式已存在", 
                            $"桌面上已存在 {shortcutName} 的快捷方式。\n是否覆盖现有快捷方式？", "覆盖", "取消");
                        if (!result) return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"显示快捷方式存在提示对话框失败: {ex}");
                }
            }

            // Icon Logic
            // Use SafeCachePath to ensure the path is accessible by Explorer (physical path in MSIX)
            string cacheDir = Path.Combine(AppEnvironment.SafeCachePath, "Shortcuts");
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

            string iconPath = Helpers.ShortcutHelper.PrepareDefaultAppIcon(cacheDir);
            
            // Try to save server icon
            if (!string.IsNullOrEmpty(server.IconBase64))
            {
                try
                {
                    // SafeCachePath ensures we are working in a location backed by a real path Explorer can reach
                    string validIconName = Helpers.HashHelper.ComputeMD5(server.Address + server.Name);
                    string savedIconPath = Path.Combine(cacheDir, $"{validIconName}.ico");
                    
                    if (!File.Exists(savedIconPath))
                    {
                        string base64Data = server.IconBase64;
                        
                        // If the icon is provided as a data URI, strip the metadata prefix and keep only the base64 payload
                        if (base64Data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            int commaIndex = base64Data.IndexOf(',');
                            if (commaIndex >= 0 && commaIndex < base64Data.Length - 1)
                            {
                                base64Data = base64Data.Substring(commaIndex + 1);
                            }
                            else
                            {
                                // Invalid data URI format (comma missing or at end), skip icon decoding
                                throw new FormatException("Invalid data URI format for server icon");
                            }
                        }
                        
                        byte[] pngBytes = Convert.FromBase64String(base64Data);
                        byte[] icoBytes = Helpers.IconHelper.CreateIcoFromPng(pngBytes);
                        
                        await File.WriteAllBytesAsync(savedIconPath, icoBytes);
                    }

                    iconPath = savedIconPath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Saving server icon failed: {ex.Message}");
                }
            }

            string targetPath = Helpers.ShortcutHelper.TrimTrailingDirectorySeparator(SelectedVersion.Path);
            
            // Parse server address with IPv6 support
            string finalAddress = server.Address;
            string portPart = "25565";
            if (!string.IsNullOrEmpty(finalAddress) && finalAddress.Length > 0)
            {
                // Handle IPv6 addresses in bracket notation, e.g. [::1]:25565
                if (finalAddress.StartsWith("[", StringComparison.Ordinal))
                {
                    int endBracket = finalAddress.IndexOf(']');
                    if (endBracket > 0)
                    {
                        string hostPart = finalAddress.Substring(1, endBracket - 1);
                        string remainder = endBracket + 1 < finalAddress.Length
                            ? finalAddress.Substring(endBracket + 1)
                            : string.Empty;

                        if (remainder.StartsWith(":", StringComparison.Ordinal))
                        {
                            string portCandidate = remainder.Substring(1);
                            // We only need to validate that portCandidate is a valid integer, not use the parsed value
                            if (int.TryParse(portCandidate, out int _))
                            {
                                portPart = portCandidate;
                            }
                        }

                        finalAddress = hostPart;
                    }
                }
                else
                {
                    // Non-bracketed form: treat single ':' as host:port, but avoid
                    // breaking unbracketed IPv6 literals with multiple colons.
                    int firstColon = finalAddress.IndexOf(':');
                    int lastColon = finalAddress.LastIndexOf(':');
                    if (firstColon > 0 && firstColon == lastColon)
                    {
                        string hostPart = finalAddress.Substring(0, lastColon);
                        string portCandidate = lastColon + 1 < finalAddress.Length
                            ? finalAddress.Substring(lastColon + 1)
                            : string.Empty;

                        // We only need to validate that portCandidate is a valid integer, not use the parsed value
                        if (!string.IsNullOrEmpty(portCandidate) && int.TryParse(portCandidate, out int _))
                        {
                            finalAddress = hostPart;
                            portPart = portCandidate;
                        }
                    }
                }
            }

            string encodedPath = Uri.EscapeDataString(targetPath ?? string.Empty);
            string encodedServer = Uri.EscapeDataString(finalAddress ?? string.Empty);
            string url = $"xianyulauncher://launch/?path={encodedPath}&server={encodedServer}&port={portPart}";
            
            // Validate URL
            if (!Helpers.ShortcutHelper.ValidateShortcutUrl(url))
            {
                throw new InvalidOperationException("Invalid shortcut URL constructed for server.");
            }

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("[InternetShortcut]");
            builder.AppendLine($"URL={url}");
            builder.AppendLine($"IconIndex=0");
            builder.AppendLine($"IconFile={iconPath}");

            await File.WriteAllTextAsync(shortcutPath, builder.ToString());
            
            StatusMessage = $"快捷方式已创建: {shortcutName}";
            
            // 提示用户
            App.MainWindow.DispatcherQueue.TryEnqueue(async () => 
            {
                await new ContentDialog
                {
                    Title = "快捷方式已创建",
                    Content = $"已在桌面创建 {shortcutName} 的快捷方式。\n双击可直接连接此服务器。",
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style
                }.ShowAsync();
            });
        }
        catch (Exception ex)
        {
            StatusMessage = "创建快捷方式失败";
            System.Diagnostics.Debug.WriteLine($"创建服务器快捷方式失败: {ex}");
            
            // Show user-friendly error message
            try
            {
                var dialogService = App.GetService<IDialogService>();
                await dialogService?.ShowMessageDialogAsync("创建失败", "创建快捷方式失败，请检查桌面权限或稍后重试。");
            }
            catch (Exception dialogEx)
            {
                System.Diagnostics.Debug.WriteLine($"显示错误对话框失败: {dialogEx}");
            }
        }
    }
}
