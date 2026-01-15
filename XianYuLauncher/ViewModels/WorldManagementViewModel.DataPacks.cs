using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.ViewModels;

public partial class WorldManagementViewModel
{
    [ObservableProperty]
    private ObservableCollection<DataPackInfo> _dataPacks = new();
    
    [ObservableProperty]
    private bool _isDataPackListEmpty = true;
    
    /// <summary>
    /// 加载数据包列表
    /// </summary>
    private async Task LoadDataPacksAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadDataPacksAsync 开始");
        
        try
        {
            var dataPacksPath = Path.Combine(WorldPath, "datapacks");
            
            if (!Directory.Exists(dataPacksPath))
            {
                System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包文件夹不存在: {dataPacksPath}");
                
                // 在 UI 线程更新
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    DataPacks.Clear();
                    IsDataPackListEmpty = true;
                });
                return;
            }
            
            // 在后台线程读取数据包列表
            var dataPackList = await Task.Run(() =>
            {
                var list = new List<DataPackInfo>();
                
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 获取所有数据包文件夹和 zip 文件
                    var dataPackFolders = Directory.GetDirectories(dataPacksPath);
                    var dataPackZips = Directory.GetFiles(dataPacksPath, "*.zip");
                    
                    // 添加所有数据包文件夹
                    foreach (var folder in dataPackFolders)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var dataPackInfo = new DataPackInfo(folder);
                        list.Add(dataPackInfo);
                    }
                    
                    // 添加所有数据包 zip 文件
                    foreach (var zip in dataPackZips)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var dataPackInfo = new DataPackInfo(zip);
                        list.Add(dataPackInfo);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 找到 {list.Count} 个数据包");
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包列表加载已取消");
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包列表加载异常: {ex.Message}");
                }
                
                return list;
            }, cancellationToken);
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // 在 UI 线程更新列表
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                DataPacks.Clear();
                foreach (var dataPack in dataPackList)
                {
                    DataPacks.Add(dataPack);
                }
                IsDataPackListEmpty = DataPacks.Count == 0;
                
                System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包列表已更新，共 {DataPacks.Count} 个");
            });
            
            // 异步加载每个数据包的详细信息（图标和描述）
            foreach (var dataPack in dataPackList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = LoadDataPackDetailsAsync(dataPack, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadDataPacksAsync 已取消");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadDataPacksAsync 异常: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载数据包详细信息（图标和描述）
    /// </summary>
    private async Task LoadDataPackDetailsAsync(DataPackInfo dataPack, CancellationToken cancellationToken)
    {
        try
        {
            // 设置加载状态
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                dataPack.IsLoadingDescription = true;
            });
            
            // 在后台线程读取数据
            var details = await Task.Run(() =>
            {
                string? iconPath = null;
                string description = string.Empty;
                int packFormat = 0;
                
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    if (Directory.Exists(dataPack.FilePath))
                    {
                        // 文件夹形式的数据包
                        var packMcmetaPath = Path.Combine(dataPack.FilePath, "pack.mcmeta");
                        var iconFilePath = Path.Combine(dataPack.FilePath, "pack.png");
                        
                        if (File.Exists(iconFilePath))
                        {
                            iconPath = iconFilePath;
                        }
                        
                        if (File.Exists(packMcmetaPath))
                        {
                            var (desc, format) = ReadPackMcmeta(packMcmetaPath);
                            description = desc;
                            packFormat = format;
                        }
                    }
                    else if (File.Exists(dataPack.FilePath) && Path.GetExtension(dataPack.FilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        // ZIP 文件形式的数据包
                        using var archive = ZipFile.OpenRead(dataPack.FilePath);
                        
                        // 读取图标
                        var iconEntry = archive.GetEntry("pack.png");
                        if (iconEntry != null)
                        {
                            // 提取图标到临时文件
                            var tempIconPath = Path.Combine(Path.GetTempPath(), $"datapack_icon_{Guid.NewGuid()}.png");
                            iconEntry.ExtractToFile(tempIconPath, true);
                            iconPath = tempIconPath;
                        }
                        
                        // 读取 pack.mcmeta
                        var packMcmetaEntry = archive.GetEntry("pack.mcmeta");
                        if (packMcmetaEntry != null)
                        {
                            using var stream = packMcmetaEntry.Open();
                            using var reader = new StreamReader(stream);
                            var json = reader.ReadToEnd();
                            var (desc, format) = ParsePackMcmeta(json);
                            description = desc;
                            packFormat = format;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 读取数据包详情失败 ({dataPack.Name}): {ex.Message}");
                }
                
                return new { IconPath = iconPath, Description = description, PackFormat = packFormat };
            }, cancellationToken);
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // 在 UI 线程更新
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                dataPack.Icon = details.IconPath;
                dataPack.Description = details.Description;
                dataPack.PackFormat = details.PackFormat;
                dataPack.IsLoadingDescription = false;
                
                System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包详情已加载: {dataPack.Name}");
            });
            
            // 如果描述为空，尝试联网获取翻译
            if (string.IsNullOrEmpty(details.Description))
            {
                await TryLoadDataPackTranslationAsync(dataPack, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包详情加载已取消: {dataPack.Name}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadDataPackDetailsAsync 异常: {ex.Message}");
            
            // 取消加载状态
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                dataPack.IsLoadingDescription = false;
            });
        }
    }
    
    /// <summary>
    /// 尝试联网获取数据包翻译
    /// </summary>
    private async Task TryLoadDataPackTranslationAsync(DataPackInfo dataPack, CancellationToken cancellationToken)
    {
        try
        {
            // TODO: 实现联网获取数据包描述翻译
            // 这里可以调用 TranslationService 来获取翻译
            // 暂时留空，后续实现
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 获取数据包翻译失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 读取 pack.mcmeta 文件
    /// </summary>
    private (string Description, int PackFormat) ReadPackMcmeta(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return ParsePackMcmeta(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 读取 pack.mcmeta 失败: {ex.Message}");
            return (string.Empty, 0);
        }
    }
    
    /// <summary>
    /// 解析 pack.mcmeta JSON
    /// </summary>
    private (string Description, int PackFormat) ParsePackMcmeta(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            if (root.TryGetProperty("pack", out var pack))
            {
                var description = string.Empty;
                var packFormat = 0;
                
                if (pack.TryGetProperty("description", out var descElement))
                {
                    // description 可能是字符串或对象
                    if (descElement.ValueKind == JsonValueKind.String)
                    {
                        description = descElement.GetString() ?? string.Empty;
                    }
                    else if (descElement.ValueKind == JsonValueKind.Object)
                    {
                        // 如果是对象，尝试获取 text 字段
                        if (descElement.TryGetProperty("text", out var textElement))
                        {
                            description = textElement.GetString() ?? string.Empty;
                        }
                    }
                }
                
                if (pack.TryGetProperty("pack_format", out var formatElement))
                {
                    packFormat = formatElement.GetInt32();
                }
                
                return (description, packFormat);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 解析 pack.mcmeta 失败: {ex.Message}");
        }
        
        return (string.Empty, 0);
    }
    
    /// <summary>
    /// 打开数据包文件夹命令
    /// </summary>
    [RelayCommand]
    private void OpenDataPackFolder()
    {
        try
        {
            var dataPacksPath = Path.Combine(WorldPath, "datapacks");
            
            if (!Directory.Exists(dataPacksPath))
            {
                Directory.CreateDirectory(dataPacksPath);
            }
            
            System.Diagnostics.Process.Start("explorer.exe", dataPacksPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打开数据包文件夹失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 删除数据包命令
    /// </summary>
    [RelayCommand]
    private async Task DeleteDataPackAsync(DataPackInfo dataPack)
    {
        if (dataPack == null)
        {
            return;
        }
        
        try
        {
            // 删除数据包（文件夹或文件）
            if (Directory.Exists(dataPack.FilePath))
            {
                Directory.Delete(dataPack.FilePath, true);
            }
            else if (File.Exists(dataPack.FilePath))
            {
                File.Delete(dataPack.FilePath);
            }
            
            // 从列表中移除
            DataPacks.Remove(dataPack);
            IsDataPackListEmpty = DataPacks.Count == 0;
            
            System.Diagnostics.Debug.WriteLine($"已删除数据包: {dataPack.Name}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"删除数据包失败：{ex.Message}");
        }
    }
}
