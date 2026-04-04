using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.WorldManagement.Models;

namespace XianYuLauncher.Features.WorldManagement.ViewModels;

public partial class WorldManagementViewModel
{
    private const int MaxConcurrentDataPackDetailLoads = 4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDataPackEmptyState))]
    private ObservableCollection<DataPackInfo> _dataPacks = new();
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDataPackEmptyState))]
    private bool _isDataPackListEmpty = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDataPackEmptyState))]
    private bool _isLoadingDataPacks;

    /// <summary>
    /// 是否显示空列表状态（仅当列表为空且不在加载时显示）
    /// </summary>
    public bool ShowDataPackEmptyState => IsDataPackListEmpty && !IsLoadingDataPacks;
    
    /// <summary>
    /// 加载数据包列表
    /// </summary>
    private async Task LoadDataPacksAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadDataPacksAsync 开始");
        IsLoadingDataPacks = true;
        
        try
        {
            var dataPacksPath = Path.Combine(WorldPath, MinecraftPathConsts.Datapacks);
            
            if (!Directory.Exists(dataPacksPath))
            {
                System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包文件夹不存在: {dataPacksPath}");
                
                // 在 UI 线程更新
                _uiDispatcher.TryEnqueue(() =>
                {
                    DataPacks.Clear();
                    IsDataPackListEmpty = true;
                    IsLoadingDataPacks = false;
                });
                return;
            }
            
            // 先读取基础列表并立即呈现，详情采用渐进补齐。
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
                        dataPackInfo.IsLoadingDescription = true;
                        list.Add(dataPackInfo);
                    }
                    
                    // 添加所有数据包 zip 文件
                    foreach (var zip in dataPackZips)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var dataPackInfo = new DataPackInfo(zip);
                        dataPackInfo.IsLoadingDescription = true;
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
            
            // 先在 UI 线程展示基础列表，避免长时间空白。
            _uiDispatcher.TryEnqueue(() =>
            {
                DataPacks.Clear();
                foreach (var dataPack in dataPackList)
                {
                    DataPacks.Add(dataPack);
                }
                IsDataPackListEmpty = DataPacks.Count == 0;
                
                System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包列表已更新，共 {DataPacks.Count} 个");
            });

            if (dataPackList.Count == 0)
            {
                _uiDispatcher.TryEnqueue(() =>
                {
                    IsLoadingDataPacks = false;
                });
                return;
            }

            await LoadDataPackDetailsIncrementallyAsync(dataPackList, cancellationToken);

            _uiDispatcher.TryEnqueue(() =>
            {
                IsLoadingDataPacks = false;
            });
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadDataPacksAsync 已取消");
            _uiDispatcher.TryEnqueue(() =>
            {
                IsLoadingDataPacks = false;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadDataPacksAsync 异常: {ex.Message}");
            _uiDispatcher.TryEnqueue(() =>
            {
                IsLoadingDataPacks = false;
            });
        }
    }

    private async Task LoadDataPackDetailsIncrementallyAsync(
        IReadOnlyCollection<DataPackInfo> dataPackList,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrentDataPackDetailLoads);

        var loadTasks = dataPackList.Select(async dataPack =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await LoadDataPackDetailsAsync(dataPack, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(loadTasks);
    }
    
    /// <summary>
    /// 加载数据包详细信息（图标和描述）
    /// </summary>
    private async Task LoadDataPackDetailsAsync(DataPackInfo dataPack, CancellationToken cancellationToken)
    {
        try
        {
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
                        // 文件夹形式的数据包（官方为 pack.png，少数资源包曾用 icon.png）
                        var packMcmetaPath = Path.Combine(dataPack.FilePath, "pack.mcmeta");
                        var packPngPath = Path.Combine(dataPack.FilePath, "pack.png");
                        var iconLegacyPath = Path.Combine(dataPack.FilePath, "icon.png");

                        if (File.Exists(packPngPath))
                        {
                            iconPath = packPngPath;
                        }
                        else if (File.Exists(iconLegacyPath))
                        {
                            iconPath = iconLegacyPath;
                        }
                        
                        if (File.Exists(packMcmetaPath))
                        {
                            var (desc, format) = ReadPackMcmeta(packMcmetaPath);
                            description = desc;
                            packFormat = format;
                        }
                    }
                    else if (File.Exists(dataPack.FilePath) && Path.GetExtension(dataPack.FilePath).Equals(FileExtensionConsts.Zip, StringComparison.OrdinalIgnoreCase))
                    {
                        // ZIP 文件形式的数据包
                        using var archive = ZipFile.OpenRead(dataPack.FilePath);
                        
                        var imageEntry = archive.GetEntry("pack.png") ?? archive.GetEntry("icon.png");
                        if (imageEntry != null)
                        {
                            var cachePath = Path.Combine(Path.GetTempPath(), "XianYuLauncher", "datapack_icons");
                            Directory.CreateDirectory(cachePath);
                            
                            var iconFileName = $"{Path.GetFileNameWithoutExtension(dataPack.FilePath)}_pack.png";
                            var tempIconPath = Path.Combine(cachePath, iconFileName);
                            
                            if (!File.Exists(tempIconPath) || new FileInfo(tempIconPath).Length == 0)
                            {
                                imageEntry.ExtractToFile(tempIconPath, true);
                            }
                            
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
            _uiDispatcher.TryEnqueue(() =>
            {
                // 加载图标
                if (!string.IsNullOrEmpty(details.IconPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.UriSource = new Uri($"file:///{details.IconPath.Replace('\\', '/')}");
                        dataPack.Icon = bitmap;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorldManagement] 加载图标失败 ({dataPack.Name}): {ex.Message}");
                    }
                }
                
                // 暂时不设置描述，等待翻译加载
                // 原始描述会在翻译加载失败时由 TryLoadDataPackMetadataAsync 设置
                dataPack.PackFormat = details.PackFormat;
                
                // 保存原始描述，供翻译失败时使用
                dataPack.Tag = details.Description;
                
                System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包详情已加载: {dataPack.Name}");
            });
            
            // 尝试从 Modrinth/CurseForge 获取元数据和翻译
            await TryLoadDataPackMetadataAsync(dataPack, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包详情加载已取消: {dataPack.Name}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadDataPackDetailsAsync 异常: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 尝试从 Modrinth/CurseForge 获取数据包元数据和翻译
    /// </summary>
    private async Task TryLoadDataPackMetadataAsync(DataPackInfo dataPack, CancellationToken cancellationToken)
    {
        try
        {
            // 设置加载状态
            _uiDispatcher.TryEnqueue(() =>
            {
                dataPack.IsLoadingDescription = true;
            });
            
            // 使用 ModInfoService 获取元数据（包含缓存）
            var metadata = await _modInfoService.GetModInfoAsync(dataPack.FilePath, cancellationToken);
            
            if (metadata != null && !string.IsNullOrEmpty(metadata.Description))
            {
                // 成功获取翻译，更新数据包信息
                _uiDispatcher.TryEnqueue(() =>
                {
                    dataPack.Description = metadata.Description;
                    dataPack.Source = metadata.Source;
                    dataPack.IsLoadingDescription = false;
                });
            }
            else
            {
                // 翻译获取失败，使用原始描述
                _uiDispatcher.TryEnqueue(() =>
                {
                    // 从 Tag 中获取原始描述
                    if (dataPack.Tag is string originalDescription && !string.IsNullOrEmpty(originalDescription))
                    {
                        dataPack.Description = originalDescription;
                    }
                    dataPack.IsLoadingDescription = false;
                });
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 数据包元数据加载已取消: {dataPack.Name}");
            _uiDispatcher.TryEnqueue(() =>
            {
                // 使用原始描述
                if (dataPack.Tag is string originalDescription && !string.IsNullOrEmpty(originalDescription))
                {
                    dataPack.Description = originalDescription;
                }
                dataPack.IsLoadingDescription = false;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 获取数据包元数据失败: {ex.Message}");
            _uiDispatcher.TryEnqueue(() =>
            {
                // 使用原始描述
                if (dataPack.Tag is string originalDescription && !string.IsNullOrEmpty(originalDescription))
                {
                    dataPack.Description = originalDescription;
                }
                dataPack.IsLoadingDescription = false;
            });
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
            var dataPacksPath = Path.Combine(WorldPath, MinecraftPathConsts.Datapacks);
            
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
