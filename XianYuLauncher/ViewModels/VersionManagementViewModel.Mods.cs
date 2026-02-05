using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Models.VersionManagement;


namespace XianYuLauncher.ViewModels;

public partial class VersionManagementViewModel
{
    [RelayCommand]
    private async Task NavigateToModDetails(ModInfo mod)
    {
        if (mod == null) return;

        // 如果没有ProjectId，尝试重新加载
        if (string.IsNullOrEmpty(mod.ProjectId))
        {
            mod.IsLoadingDescription = true;
            try 
            {
               await LoadModDescriptionAsync(mod, default);
            }
            finally
            {
               mod.IsLoadingDescription = false;
            }
        }

        if (!string.IsNullOrEmpty(mod.ProjectId))
        {
            string navigationId = mod.ProjectId;
            if (mod.Source == "CurseForge" && !navigationId.StartsWith("curseforge-"))
            {
                navigationId = "curseforge-" + navigationId;
            }
            
            _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName, navigationId);
        }
        else
        {
             StatusMessage = "无法获取该Mod的详细信息（未在Modrinth或CurseForge找到）";
        }
    }

    #region Mod管理

    /// <summary>
    /// Mod管理相关的ViewModel逻辑
    /// </summary>
    [ObservableProperty]
    private bool _isModSelectionModeEnabled;

    [RelayCommand]
    private void ToggleModSelectionMode()
    {
        IsModSelectionModeEnabled = !IsModSelectionModeEnabled;
        if (!IsModSelectionModeEnabled)
        {
            // 退出选择模式时清楚所有选中状态
            foreach (var mod in Mods)
            {
                mod.IsSelected = false;
            }
        }
    }

    [RelayCommand]
    private void SelectAllMods()
    {
        if (IsModListEmpty) return;
        
        bool allSelected = Mods.All(m => m.IsSelected);
        foreach (var mod in Mods)
        {
            mod.IsSelected = !allSelected;
        }
    }

    /// <summary>
        /// 检查本地图标是否存在并返回图标路径
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <param name="resourceType">资源类型（mods, resourcepacks, shaderpacks, datapacks, maps）</param>
        /// <returns>图标路径，如果不存在则返回null</returns>
        private string GetLocalIconPath(string filePath, string resourceType)
        {
            try
            {
                // 获取启动器缓存路径
                string cachePath = _fileService.GetLauncherCachePath();
                // 构建图标目录路径
                string iconDir = Path.Combine(cachePath, "icons", resourceType);
                
                // 创建图标目录（如果不存在）
                Directory.CreateDirectory(iconDir);
                
                // 获取文件名
                string fileName = Path.GetFileName(filePath);
                // 复制一份用于处理
                string baseFileName = fileName;
                
                // 去掉.disabled后缀（如果存在）
                if (baseFileName.EndsWith(".disabled"))
                {
                    baseFileName = baseFileName.Substring(0, baseFileName.Length - ".disabled".Length);
                }
                
                // 去掉文件扩展名
                string fileBaseName = Path.GetFileNameWithoutExtension(baseFileName);
                
                // 搜索匹配的图标文件
                // 1. 搜索普通图标（格式：*_fileName_icon.png）
                string[] iconFiles = Directory.GetFiles(iconDir, $"*_{fileBaseName}_icon.png");
                if (iconFiles.Length > 0)
                {
                    // 验证图标文件是否有效（大小大于0）
                    foreach (var iconFile in iconFiles)
                    {
                        if (IsValidIconFile(iconFile))
                        {
                            return iconFile;
                        }
                        else
                        {
                            // 删除损坏的图标文件
                            // System.Diagnostics.Debug.WriteLine($"删除损坏的图标文件: {iconFile}");
                            try { File.Delete(iconFile); } catch { }
                        }
                    }
                }
                
                // 2. 搜索从Modrinth下载的图标（格式：modrinth_fileName_icon.png）
                string modrinthIconPattern = Path.Combine(iconDir, $"modrinth_{fileBaseName}_icon.png");
                if (File.Exists(modrinthIconPattern))
                {
                    if (IsValidIconFile(modrinthIconPattern))
                    {
                        // System.Diagnostics.Debug.WriteLine($"找到Modrinth图标: {modrinthIconPattern}");
                        return modrinthIconPattern;
                    }
                    else
                    {
                        // System.Diagnostics.Debug.WriteLine($"删除损坏的Modrinth图标: {modrinthIconPattern}");
                        try { File.Delete(modrinthIconPattern); } catch { }
                    }
                }
                
                // 3. 搜索从CurseForge下载的图标（格式：curseforge_fileName_icon.png）
                string curseForgeIconPattern = Path.Combine(iconDir, $"curseforge_{fileBaseName}_icon.png");
                if (File.Exists(curseForgeIconPattern))
                {
                    if (IsValidIconFile(curseForgeIconPattern))
                    {
                        // System.Diagnostics.Debug.WriteLine($"找到CurseForge图标: {curseForgeIconPattern}");
                        return curseForgeIconPattern;
                    }
                    else
                    {
                        // System.Diagnostics.Debug.WriteLine($"删除损坏的CurseForge图标: {curseForgeIconPattern}");
                        try { File.Delete(curseForgeIconPattern); } catch { }
                    }
                }
                
                // 4. 对于资源包，尝试从 zip 文件中提取 pack.png
                if (resourceType == "resourcepack" && File.Exists(filePath) && 
                    (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                     filePath.EndsWith(".mcpack", StringComparison.OrdinalIgnoreCase)))
                {
                    string extractedIconPath = ExtractResourcePackIcon(filePath, iconDir, fileBaseName);
                    if (!string.IsNullOrEmpty(extractedIconPath))
                    {
                        // System.Diagnostics.Debug.WriteLine($"从资源包中提取图标: {extractedIconPath}");
                        return extractedIconPath;
                    }
                }
            }
            catch (Exception ex)
            {
                // 忽略错误，返回null
                System.Diagnostics.Debug.WriteLine("获取本地图标失败: " + ex.Message);
            }
            
            // 返回null，表示没有本地图标
            return null;
        }
        
        /// <summary>
        /// 验证图标文件是否有效
        /// </summary>
        private bool IsValidIconFile(string iconPath)
        {
            try
            {
                if (!File.Exists(iconPath))
                    return false;
                
                var fileInfo = new FileInfo(iconPath);
                // 检查文件大小是否大于100字节（一个有效的PNG至少应该有这么大）
                return fileInfo.Length > 100;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 从资源包 zip 文件中提取 pack.png 图标
        /// </summary>
        /// <param name="zipFilePath">资源包 zip 文件路径</param>
        /// <param name="iconDir">图标保存目录</param>
        /// <param name="fileBaseName">文件基础名称</param>
        /// <returns>提取的图标路径，如果失败则返回 null</returns>
        private string ExtractResourcePackIcon(string zipFilePath, string iconDir, string fileBaseName)
        {
            try
            {
                // 构建缓存的图标路径
                string cachedIconPath = Path.Combine(iconDir, $"local_{fileBaseName}_icon.png");
                
                // 如果已经提取过，直接返回
                if (File.Exists(cachedIconPath))
                {
                    return cachedIconPath;
                }
                
                // 打开 zip 文件
                using (var zipArchive = ZipFile.OpenRead(zipFilePath))
                {
                    // 查找 pack.png 文件
                    var packPngEntry = zipArchive.GetEntry("pack.png");
                    if (packPngEntry != null)
                    {
                        // 提取到缓存目录
                        using (var entryStream = packPngEntry.Open())
                        using (var fileStream = File.Create(cachedIconPath))
                        {
                            entryStream.CopyTo(fileStream);
                        }
                        
                        return cachedIconPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从资源包提取图标失败: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 计算文件的SHA1哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>SHA1哈希值</returns>
        private string CalculateSHA1(string filePath)
        {
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha1.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// 从Modrinth API获取mod图标URL
        /// </summary>
        /// <param name="filePath">mod文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>图标URL，如果获取失败则返回null</returns>
        private async Task<string> GetModrinthIconUrlAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 计算文件的SHA1哈希值
                string sha1Hash = CalculateSHA1(filePath);
                System.Diagnostics.Debug.WriteLine($"计算SHA1哈希值: {sha1Hash}");

                // 构建请求体
                var requestBody = new
                {
                    hashes = new[] { sha1Hash },
                    algorithm = "sha1"
                };

                // 调用Modrinth API的POST /version_files端点
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", GetModrinthUserAgent());
                    
                    string versionFilesUrl = TransformModrinthApiUrl("https://api.modrinth.com/v2/version_files");
                    var content = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json");
                    
                    System.Diagnostics.Debug.WriteLine($"调用Modrinth API: {versionFilesUrl}");
                    var response = await httpClient.PostAsync(versionFilesUrl, content, cancellationToken);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        System.Diagnostics.Debug.WriteLine($"API响应: {responseContent}");
                        
                        // 解析响应
                        var versionResponse = System.Text.Json.JsonSerializer.Deserialize<
                            System.Collections.Generic.Dictionary<string, VersionInfo>
                        >(responseContent);
                        
                        if (versionResponse != null && versionResponse.ContainsKey(sha1Hash))
                        {
                            VersionInfo versionInfo = versionResponse[sha1Hash];
                            string projectId = versionInfo.project_id;
                            
                            System.Diagnostics.Debug.WriteLine($"获取到project_id: {projectId}");
                            
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // 调用Modrinth API的GET /project/{id}端点
                            string projectUrl = TransformModrinthApiUrl($"https://api.modrinth.com/v2/project/{projectId}");
                            System.Diagnostics.Debug.WriteLine($"调用Modrinth API获取项目信息: {projectUrl}");
                            var projectResponse = await httpClient.GetAsync(projectUrl, cancellationToken);
                            
                            if (projectResponse.IsSuccessStatusCode)
                            {
                                string projectContent = await projectResponse.Content.ReadAsStringAsync(cancellationToken);
                                System.Diagnostics.Debug.WriteLine($"项目API响应: {projectContent}");
                                
                                // 解析项目响应
                                var projectInfo = System.Text.Json.JsonSerializer.Deserialize<ProjectInfo>(projectContent);
                                
                                if (projectInfo != null && !string.IsNullOrEmpty(projectInfo.icon_url))
                                {
                                    System.Diagnostics.Debug.WriteLine($"获取到icon_url: {projectInfo.icon_url}");
                                    return projectInfo.icon_url;
                                }
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"API调用失败: {response.StatusCode}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从Modrinth获取图标失败: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 保存Modrinth图标到本地
        /// </summary>
        /// <param name="filePath">资源文件路径</param>
        /// <param name="iconUrl">图标URL</param>
        /// <param name="resourceType">资源类型</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>本地图标路径，如果保存失败则返回null</returns>
        private async Task<string> SaveModrinthIconAsync(string filePath, string iconUrl, string resourceType, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 获取启动器缓存路径
                string cachePath = _fileService.GetLauncherCachePath();
                // 构建图标目录路径
                string iconDir = Path.Combine(cachePath, "icons", resourceType);
                Directory.CreateDirectory(iconDir);
                
                // 获取文件名
                string fileName = Path.GetFileName(filePath);
                // 去掉.disabled后缀（如果存在）
                if (fileName.EndsWith(".disabled"))
                {
                    fileName = fileName.Substring(0, fileName.Length - ".disabled".Length);
                }
                // 去掉文件扩展名
                string fileBaseName = Path.GetFileNameWithoutExtension(fileName);
                
                // 生成唯一图标文件名
                string iconFileName = $"modrinth_{fileBaseName}_icon.png";
                string iconFilePath = Path.Combine(iconDir, iconFileName);
                
                // 下载并保存图标（带超时）
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10); // 10秒超时
                    System.Diagnostics.Debug.WriteLine($"下载图标: {iconUrl}");
                    byte[] iconBytes = await httpClient.GetByteArrayAsync(iconUrl, cancellationToken);
                    await File.WriteAllBytesAsync(iconFilePath, iconBytes, cancellationToken);
                    System.Diagnostics.Debug.WriteLine($"图标保存到本地: {iconFilePath}");
                    
                    return iconFilePath;
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存Modrinth图标失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 版本信息类，用于解析Modrinth API响应
        /// </summary>
        private class VersionInfo
        {
            public string project_id { get; set; }
        }

        /// <summary>
        /// 项目信息类，用于解析Modrinth API响应
        /// </summary>
        private class ProjectInfo
        {
            public string icon_url { get; set; }
        }

        /// <summary>
        /// 从CurseForge API获取mod图标URL
        /// </summary>
        /// <param name="filePath">mod文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>图标URL，如果获取失败则返回null</returns>
        private async Task<string> GetCurseForgeIconUrlAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 计算文件的CurseForge Fingerprint
                uint fingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(filePath);
                System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 计算Fingerprint: {fingerprint}");

                cancellationToken.ThrowIfCancellationRequested();
                
                // 调用CurseForge API查询Fingerprint
                var result = await _curseForgeService.GetFingerprintMatchesAsync(new List<uint> { fingerprint });
                
                if (result?.ExactMatches != null && result.ExactMatches.Count > 0)
                {
                    var match = result.ExactMatches[0];
                    System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 找到匹配的Mod ID: {match.Id}");
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 获取Mod详情以获取Logo信息
                    var modDetail = await _curseForgeService.GetModDetailAsync(match.Id);
                    
                    if (modDetail?.Logo != null && !string.IsNullOrEmpty(modDetail.Logo.ThumbnailUrl))
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 获取到图标URL: {modDetail.Logo.ThumbnailUrl}");
                        return modDetail.Logo.ThumbnailUrl;
                    }
                    else if (modDetail?.Logo != null && !string.IsNullOrEmpty(modDetail.Logo.Url))
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 获取到图标URL: {modDetail.Logo.Url}");
                        return modDetail.Logo.Url;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 未找到匹配的Mod");
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 从CurseForge获取图标失败: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 保存CurseForge图标到本地
        /// </summary>
        /// <param name="filePath">资源文件路径</param>
        /// <param name="iconUrl">图标URL</param>
        /// <param name="resourceType">资源类型</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>本地图标路径，如果保存失败则返回null</returns>
        private async Task<string> SaveCurseForgeIconAsync(string filePath, string iconUrl, string resourceType, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 获取启动器缓存路径
                string cachePath = _fileService.GetLauncherCachePath();
                // 构建图标目录路径
                string iconDir = Path.Combine(cachePath, "icons", resourceType);
                Directory.CreateDirectory(iconDir);
                
                // 获取文件名
                string fileName = Path.GetFileName(filePath);
                // 去掉.disabled后缀（如果存在）
                if (fileName.EndsWith(".disabled"))
                {
                    fileName = fileName.Substring(0, fileName.Length - ".disabled".Length);
                }
                // 去掉文件扩展名
                string fileBaseName = Path.GetFileNameWithoutExtension(fileName);
                
                // 生成唯一图标文件名
                string iconFileName = $"curseforge_{fileBaseName}_icon.png";
                string iconFilePath = Path.Combine(iconDir, iconFileName);
                
                // 下载并保存图标（带超时）
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10); // 10秒超时
                    System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 下载图标: {iconUrl}");
                    byte[] iconBytes = await httpClient.GetByteArrayAsync(iconUrl, cancellationToken);
                    await File.WriteAllBytesAsync(iconFilePath, iconBytes, cancellationToken);
                    System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 图标保存到本地: {iconFilePath}");
                    
                    return iconFilePath;
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 保存CurseForge图标失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 异步加载并更新单个资源的图标
        /// </summary>
        /// <param name="iconProperty">图标属性的Action委托</param>
        /// <param name="filePath">资源文件路径</param>
        /// <param name="resourceType">资源类型</param>
        /// <param name="isModrinthSupported">是否支持从Modrinth API获取</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task LoadResourceIconAsync(Action<string> iconProperty, string filePath, string resourceType, bool isModrinthSupported = false, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 检查本地图标
                string localIcon = GetLocalIconPath(filePath, resourceType);
                if (!string.IsNullOrEmpty(localIcon))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 确保在 UI 线程上更新属性
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            // 再次检查是否已取消
                            if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                            {
                                return;
                            }
                            iconProperty(localIcon);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"设置图标失败: {ex.Message}");
                        }
                    });
                    return;
                }
                
                // 如果支持Modrinth且本地没有图标，尝试从Modrinth API获取
                if (isModrinthSupported)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    System.Diagnostics.Debug.WriteLine($"本地没有图标，尝试从Modrinth API获取{resourceType}图标: {filePath}");
                    string iconUrl = await GetModrinthIconUrlAsync(filePath, cancellationToken);
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // 保存图标到本地，传递资源类型
                        string localIconPath = await SaveModrinthIconAsync(filePath, iconUrl, resourceType, cancellationToken);
                        if (!string.IsNullOrEmpty(localIconPath))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // 确保在 UI 线程上更新属性
                            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    // 再次检查是否已取消
                                    if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                                    {
                                        return;
                                    }
                                    iconProperty(localIconPath);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"设置图标失败: {ex.Message}");
                                }
                            });
                            return;
                        }
                    }
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Modrinth 失败，尝试 CurseForge
                    System.Diagnostics.Debug.WriteLine($"Modrinth未找到图标，尝试从CurseForge API获取{resourceType}图标: {filePath}");
                    string curseForgeIconUrl = await GetCurseForgeIconUrlAsync(filePath, cancellationToken);
                    if (!string.IsNullOrEmpty(curseForgeIconUrl))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // 保存图标到本地，传递资源类型
                        string localIconPath = await SaveCurseForgeIconAsync(filePath, curseForgeIconUrl, resourceType, cancellationToken);
                        if (!string.IsNullOrEmpty(localIconPath))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // 确保在 UI 线程上更新属性
                            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    // 再次检查是否已取消
                                    if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                                    {
                                        return;
                                    }
                                    iconProperty(localIconPath);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"设置图标失败: {ex.Message}");
                                }
                            });
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 加载{resourceType}图标已取消: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载{resourceType}图标失败: {ex.Message}");
            }
        }
        


        /// <summary>
        /// 是否显示转移Mod弹窗
        /// </summary>
        /// <summary>
        /// 转移选中的Mods到其他版本
        /// </summary>
        [RelayCommand]
        private async Task MoveModsToOtherVersionAsync()
        {
            try
            {
                // 获取选中的Mods
                var selectedMods = Mods.Where(mod => mod.IsSelected).ToList();
                if (selectedMods.Count == 0)
                {
                    StatusMessage = "请先选择要转移的Mod";
                    return;
                }

                // 保存选中的Mods，用于后续转移
                _selectedModsForMove = selectedMods;

                // 加载所有已安装的版本
                await LoadTargetVersionsAsync();
                
                // 设置类型并显示对话框
                CurrentResourceMoveType = ResourceMoveType.Mod;
                IsMoveResourcesDialogVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
                StatusMessage = $"转移Mod失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 保存选中的Mods，用于转移
        /// </summary>
        private List<ModInfo> _selectedModsForMove;
        
        /// <summary>
        /// 确认转移Mods到目标版本
        /// </summary>
        private async Task ConfirmMoveModsAsync()
        {
            if (SelectedTargetVersion == null || _selectedModsForMove == null || _selectedModsForMove.Count == 0)
            {
                StatusMessage = "请选择要转移的Mod和目标版本";
                return;
            }
            
            try
            {
                // 设置下载状态
                IsDownloading = true;
                DownloadProgressDialogTitle = "VersionManagerPage_MigratingModsText".GetLocalized();
                DownloadProgress = 0;
                CurrentDownloadItem = string.Empty;
                StatusMessage = "VersionManagerPage_PreparingModTransferText".GetLocalized();
                
                // 记录转移结果
                var moveResults = new List<MoveModResult>();
                
                // 获取源版本和目标版本的信息
                string sourceVersionPath = GetVersionSpecificPath("mods");
                string targetVersion = SelectedTargetVersion.VersionName;
                
                // 设置目标版本的上下文
                var originalSelectedVersion = SelectedVersion;
                
                // 获取所有已安装版本，用于查找目标版本
                var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
                SelectedVersion = new VersionListViewModel.VersionInfoItem
                {
                    Name = targetVersion,
                    Path = Path.Combine(_fileService.GetMinecraftDataPath(), "versions", targetVersion)
                };
                
                if (SelectedVersion == null || !Directory.Exists(SelectedVersion.Path))
                {
                    throw new Exception($"无法找到目标版本: {targetVersion}");
                }
                
                string targetVersionPath = GetVersionSpecificPath("mods");
                
                // 获取目标版本的ModLoader和游戏版本
                string modLoader = "fabric"; // 默认fabric
                string gameVersion = SelectedVersion?.VersionNumber ?? "1.19.2";
                
                // 使用VersionInfoService获取完整的版本配置信息
                var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                if (versionInfoService != null && SelectedVersion != null)
                {
                    string versionDir = Path.Combine(SelectedVersion.Path);
                    Core.Models.VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(SelectedVersion.Name, versionDir);
                    
                    if (versionConfig != null)
                    {
                        // 获取ModLoader类型
                        if (!string.IsNullOrEmpty(versionConfig.ModLoaderType))
                        {
                            modLoader = versionConfig.ModLoaderType.ToLower();
                        }
                        else
                        {
                            // 回退到基于版本名的判断
                            if (SelectedVersion.Name.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "fabric";
                            }
                            else if (SelectedVersion.Name.Contains("forge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "forge";
                            }
                            else if (SelectedVersion.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "neoforge";
                            }
                            else if (SelectedVersion.Name.Contains("quilt", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "quilt";
                            }
                        }
                        
                        // 获取游戏版本
                        if (!string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                        {
                            gameVersion = versionConfig.MinecraftVersion;
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"转移Mod到目标版本: {targetVersion}");
                System.Diagnostics.Debug.WriteLine($"目标版本信息：ModLoader={modLoader}, GameVersion={gameVersion}");
                
                // 遍历每个选中的Mod
                for (int i = 0; i < _selectedModsForMove.Count; i++)
                {
                    var mod = _selectedModsForMove[i];
                    var result = new MoveModResult
                    {
                        ModName = mod.Name,
                        SourcePath = mod.FilePath,
                        Status = MoveModStatus.Failed
                    };
                    
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"正在处理Mod: {mod.Name}");
                        
                        // 第一步：尝试通过 Modrinth 处理
                        bool modrinthSuccess = await TryMoveModViaModrinthAsync(mod, modLoader, gameVersion, targetVersionPath, result);
                        
                        // 第二步：如果 Modrinth 失败，尝试通过 CurseForge 处理
                        if (!modrinthSuccess)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MoveMod] Modrinth 失败，尝试 CurseForge: {mod.Name}");
                            bool curseForgeSuccess = await TryMoveModViaCurseForgeAsync(mod, modLoader, gameVersion, targetVersionPath, result);
                            
                            // 如果 CurseForge 也失败，尝试直接复制
                            if (!curseForgeSuccess)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MoveMod] CurseForge 失败，直接复制: {mod.Name}");
                                string targetFilePath = Path.Combine(targetVersionPath, Path.GetFileName(mod.FilePath));
                                Directory.CreateDirectory(targetVersionPath);
                                File.Copy(mod.FilePath, targetFilePath, true);
                                result.Status = MoveModStatus.Copied;
                                result.TargetPath = targetFilePath;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Status = MoveModStatus.Failed;
                        result.ErrorMessage = ex.Message;
                        System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
                    }
                    
                    moveResults.Add(result);
                    
                    // 更新进度
                    DownloadProgress = (i + 1) / (double)_selectedModsForMove.Count * 100;
                }
                
                // 恢复原始选中版本
                SelectedVersion = originalSelectedVersion;
                
                // 显示转移结果
                MoveResults = moveResults;
                IsMoveResultDialogVisible = true;
                
                // 重新加载当前版本的Mod列表
                await LoadModsListOnlyAsync();
                
                // 异步加载图标，不阻塞UI
                _ = LoadAllIconsAsync(_pageCancellationTokenSource?.Token ?? default);
                
                StatusMessage = $"Mod转移完成，共处理 {moveResults.Count} 个Mod";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
                StatusMessage = $"转移Mod失败: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
                CurrentDownloadItem = string.Empty;
                IsMoveResourcesDialogVisible = false;
            }
        }
        
        /// <summary>
        /// 尝试通过 Modrinth 转移 Mod
        /// </summary>
        private async Task<bool> TryMoveModViaModrinthAsync(ModInfo mod, string modLoader, string gameVersion, string targetVersionPath, MoveModResult result)
        {
            try
            {
                // 计算Mod的SHA1哈希值
                string sha1Hash = CalculateSHA1(mod.FilePath);
                
                // 获取当前Mod版本的Modrinth信息
                ModrinthVersion modrinthVersion = await _modrinthService.GetVersionFileByHashAsync(sha1Hash);
                
                if (modrinthVersion == null)
                {
                    return false;
                }
                
                // 检查Mod是否兼容目标版本
                bool isCompatible = modrinthVersion.GameVersions.Contains(gameVersion) && 
                                  modrinthVersion.Loaders.Contains(modLoader);
                
                if (isCompatible)
                {
                    // 直接复制文件到目标版本
                    string targetFilePath = Path.Combine(targetVersionPath, Path.GetFileName(mod.FilePath));
                    Directory.CreateDirectory(targetVersionPath);
                    File.Copy(mod.FilePath, targetFilePath, true);
                    
                    result.Status = MoveModStatus.Success;
                    result.TargetPath = targetFilePath;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 成功转移Mod: {mod.Name}");
                    return true;
                }
                else
                {
                    // 尝试获取兼容目标版本的Mod版本
                    var compatibleVersions = await _modrinthService.GetProjectVersionsAsync(
                        modrinthVersion.ProjectId,
                        new List<string> { modLoader },
                        new List<string> { gameVersion });
                    
                    if (compatibleVersions != null && compatibleVersions.Count > 0)
                    {
                        var latestCompatibleVersion = compatibleVersions.OrderByDescending(v => v.DatePublished).First();
                        
                        if (latestCompatibleVersion.Files != null && latestCompatibleVersion.Files.Count > 0)
                        {
                            var primaryFile = latestCompatibleVersion.Files.FirstOrDefault(f => f.Primary) ?? latestCompatibleVersion.Files[0];
                            string downloadUrl = primaryFile.Url.AbsoluteUri;
                            string fileName = primaryFile.Filename;
                            string tempFilePath = Path.Combine(targetVersionPath, $"{fileName}.tmp");
                            string finalFilePath = Path.Combine(targetVersionPath, fileName);
                            
                            CurrentDownloadItem = fileName;
                            bool downloadSuccess = await DownloadModAsync(downloadUrl, tempFilePath);
                            
                            if (downloadSuccess)
                            {
                                if (latestCompatibleVersion.Dependencies != null && latestCompatibleVersion.Dependencies.Count > 0)
                                {
                                    await ProcessDependenciesAsync(latestCompatibleVersion.Dependencies, targetVersionPath);
                                }
                                
                                if (File.Exists(finalFilePath))
                                {
                                    File.Delete(finalFilePath);
                                }
                                File.Move(tempFilePath, finalFilePath);
                                
                                result.Status = MoveModStatus.Updated;
                                result.TargetPath = finalFilePath;
                                result.NewVersion = latestCompatibleVersion.VersionNumber;
                                System.Diagnostics.Debug.WriteLine($"[Modrinth] 成功更新并转移Mod: {mod.Name}");
                                return true;
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 转移Mod失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 尝试通过 CurseForge 转移 Mod
        /// </summary>
        private async Task<bool> TryMoveModViaCurseForgeAsync(ModInfo mod, string modLoader, string gameVersion, string targetVersionPath, MoveModResult result)
        {
            try
            {
                // 计算 CurseForge Fingerprint
                uint fingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(mod.FilePath);
                System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] Fingerprint: {fingerprint}");
                
                // 查询 Fingerprint
                var fingerprintResult = await _curseForgeService.GetFingerprintMatchesAsync(new List<uint> { fingerprint });
                
                if (fingerprintResult?.ExactMatches == null || fingerprintResult.ExactMatches.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 未找到匹配");
                    return false;
                }
                
                var match = fingerprintResult.ExactMatches[0];
                System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 找到匹配 Mod ID: {match.Id}");
                
                // 转换 ModLoader 类型为 CurseForge 格式
                int? modLoaderType = modLoader.ToLower() switch
                {
                    "forge" => 1,
                    "fabric" => 4,
                    "quilt" => 5,
                    "neoforge" => 6,
                    _ => null
                };
                
                // 检查当前文件是否兼容目标版本
                if (match.File != null && 
                    match.File.GameVersions.Contains(gameVersion) &&
                    (modLoaderType == null || match.File.GameVersions.Any(v => v.Equals(modLoader, StringComparison.OrdinalIgnoreCase))))
                {
                    // 直接复制
                    string targetFilePath = Path.Combine(targetVersionPath, Path.GetFileName(mod.FilePath));
                    Directory.CreateDirectory(targetVersionPath);
                    File.Copy(mod.FilePath, targetFilePath, true);
                    
                    result.Status = MoveModStatus.Success;
                    result.TargetPath = targetFilePath;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 成功转移Mod: {mod.Name}");
                    return true;
                }
                
                // 获取兼容版本的文件列表
                var files = await _curseForgeService.GetModFilesAsync(match.Id, gameVersion, modLoaderType);
                
                if (files != null && files.Count > 0)
                {
                    // 选择最新的 Release 版本
                    var latestFile = files
                        .Where(f => f.ReleaseType == 1) // 1 = Release
                        .OrderByDescending(f => f.FileDate)
                        .FirstOrDefault() ?? files.OrderByDescending(f => f.FileDate).First();
                    
                    if (!string.IsNullOrEmpty(latestFile.DownloadUrl))
                    {
                        string fileName = latestFile.FileName;
                        string tempFilePath = Path.Combine(targetVersionPath, $"{fileName}.tmp");
                        string finalFilePath = Path.Combine(targetVersionPath, fileName);
                        
                        CurrentDownloadItem = fileName;
                        bool downloadSuccess = await _curseForgeService.DownloadFileAsync(
                            latestFile.DownloadUrl,
                            tempFilePath,
                            (name, progress) => DownloadProgress = progress);
                        
                        if (downloadSuccess)
                        {
                            if (File.Exists(finalFilePath))
                            {
                                File.Delete(finalFilePath);
                            }
                            File.Move(tempFilePath, finalFilePath);
                            
                            result.Status = MoveModStatus.Updated;
                            result.TargetPath = finalFilePath;
                            result.NewVersion = latestFile.DisplayName;
                            System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 成功更新并转移Mod: {mod.Name}");
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 转移Mod失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 转移Mod结果类
        /// </summary>
        public enum MoveModStatus
        {
            Success,
            Updated,
            Copied,
            Incompatible,
            Failed
        }
        
        /// <summary>
        /// 转移Mod结果
        /// </summary>
        public partial class MoveModResult : ObservableObject
        {
            [ObservableProperty]
            private string _modName;
            
            [ObservableProperty]
            private string _sourcePath;
            
            [ObservableProperty]
            private string _targetPath;
            
            [ObservableProperty]
            private MoveModStatus _status;
            
            [ObservableProperty]
            private string _newVersion;
            
            [ObservableProperty]
            private string _errorMessage;
            
            /// <summary>
            /// 显示状态文本
            /// </summary>
            public string StatusText
            {
                get
                {
                    switch (Status)
                    {
                        case MoveModStatus.Success:
                            return "VersionManagerPage_ModMovedSuccessText".GetLocalized();
                        case MoveModStatus.Updated:
                            return "VersionManagerPage_UpdatedAndMovedText".GetLocalized();
                        case MoveModStatus.Copied:
                            return "VersionManagerPage_ModCopiedText".GetLocalized();
                        case MoveModStatus.Incompatible:
                            return "VersionManagerPage_ModIncompatibleText".GetLocalized();
                        case MoveModStatus.Failed:
                            return "VersionManagerPage_ModMoveFailedText".GetLocalized();
                        default:
                            return "VersionManagerPage_UnknownStatusText".GetLocalized();
                    }
                }
            }
            
            /// <summary>
            /// 是否显示为灰字
            /// </summary>
            public bool IsGrayedOut => Status == MoveModStatus.Incompatible || Status == MoveModStatus.Failed;
        }
        
        /// <summary>
        /// 转移结果列表
        /// </summary>
        [ObservableProperty]
        private List<MoveModResult> _moveResults;
        
        /// <summary>
        /// 是否显示转移结果弹窗
        /// </summary>
        [ObservableProperty]
        private bool _isMoveResultDialogVisible;
        
        /// <summary>
        /// 目标版本列表，用于转移Mod功能
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TargetVersionInfo> _targetVersions = new();
        
        /// <summary>
        /// 选中的目标版本
        /// </summary>
        [ObservableProperty]
        private TargetVersionInfo _selectedTargetVersion;
        
        /// <summary>
        /// 加载目标版本列表
        /// </summary>
        private async Task LoadTargetVersionsAsync()
        {
            TargetVersions.Clear();
            
            // 获取实际已安装的游戏版本
            var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
            
            // 处理每个已安装版本，所有版本都显示为兼容
            foreach (var installedVersion in installedVersions)
            {
                // 创建目标版本信息，所有版本都兼容
                TargetVersions.Add(new TargetVersionInfo 
                {
                    VersionName = installedVersion,
                    IsCompatible = true
                });
            }
        }
        
        /// <summary>
        /// 目标版本信息类，用于转移Mod功能
        /// </summary>
        public partial class TargetVersionInfo : ObservableObject
        {
            /// <summary>
            /// 版本名称
            /// </summary>
            [ObservableProperty]
            private string _versionName;
            
            /// <summary>
            /// 是否兼容
            /// </summary>
            [ObservableProperty]
            private bool _isCompatible;
        }

        /// 更新选中的Mods
        /// </summary>
        [RelayCommand]
        private async Task UpdateModsAsync()
        {
            try
            {
                // 获取选中的Mods
                var selectedMods = Mods.Where(mod => mod.IsSelected).ToList();
                if (selectedMods.Count == 0)
                {
                    StatusMessage = "请先选择要更新的Mod";
                    return;
                }
                
                // 设置下载状态
                IsDownloading = true;
                DownloadProgressDialogTitle = "VersionManagerPage_UpdatingModsText".GetLocalized();
                DownloadProgress = 0;
                CurrentDownloadItem = string.Empty;
                
                // 计算选中Mod的SHA1哈希值（用于Modrinth）
                var modHashes = new List<string>();
                var modFilePathMap = new Dictionary<string, string>();
                
                foreach (var mod in selectedMods)
                {
                    string sha1Hash = CalculateSHA1(mod.FilePath);
                    modHashes.Add(sha1Hash);
                    modFilePathMap[sha1Hash] = mod.FilePath;
                    System.Diagnostics.Debug.WriteLine($"Mod {mod.Name} 的SHA1哈希值: {sha1Hash}");
                }
                
                // 获取当前版本的ModLoader和游戏版本
                string modLoader = "fabric"; // 默认fabric
                string gameVersion = SelectedVersion?.VersionNumber ?? "1.19.2"; // 使用选中版本的VersionNumber
                
                // 使用VersionInfoService获取完整的版本配置信息
                var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                if (versionInfoService != null && SelectedVersion != null)
                {
                    string versionDir = Path.Combine(SelectedVersion.Path);
                    Core.Models.VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(SelectedVersion.Name, versionDir);
                    
                    if (versionConfig != null)
                    {
                        // 获取ModLoader类型
                        if (!string.IsNullOrEmpty(versionConfig.ModLoaderType))
                        {
                            modLoader = versionConfig.ModLoaderType.ToLower();
                        }
                        else
                        {
                            // 回退到基于版本名的判断
                            if (SelectedVersion.Name.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "fabric";
                            }
                            else if (SelectedVersion.Name.Contains("forge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "forge";
                            }
                            else if (SelectedVersion.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "neoforge";
                            }
                            else if (SelectedVersion.Name.Contains("quilt", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "quilt";
                            }
                        }
                        
                        // 获取游戏版本
                        if (!string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                        {
                            gameVersion = versionConfig.MinecraftVersion;
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"当前版本信息：ModLoader={modLoader}, GameVersion={gameVersion}");
                
                // 获取Mods文件夹路径
                string modsPath = GetVersionSpecificPath("mods");
                
                int updatedCount = 0;
                int upToDateCount = 0;
                
                // 第一步：尝试通过 Modrinth 更新
                System.Diagnostics.Debug.WriteLine($"[UpdateMods] 第一步：尝试通过 Modrinth 更新 {selectedMods.Count} 个Mod");
                var modrinthResult = await TryUpdateModsViaModrinthAsync(
                    modHashes, 
                    modFilePathMap, 
                    modLoader, 
                    gameVersion, 
                    modsPath);
                
                updatedCount += modrinthResult.UpdatedCount;
                upToDateCount += modrinthResult.UpToDateCount;
                
                // 第二步：对于 Modrinth 未找到的 Mod，尝试通过 CurseForge 更新
                var modrinthFailedMods = selectedMods
                    .Where(mod => !modrinthResult.ProcessedMods.Contains(mod.FilePath))
                    .ToList();
                
                if (modrinthFailedMods.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateMods] 第二步：尝试通过 CurseForge 更新 {modrinthFailedMods.Count} 个Mod");
                    var curseForgeResult = await TryUpdateModsViaCurseForgeAsync(
                        modrinthFailedMods,
                        modLoader,
                        gameVersion,
                        modsPath);
                    
                    updatedCount += curseForgeResult.UpdatedCount;
                    upToDateCount += curseForgeResult.UpToDateCount;
                }
                
                // 重新加载Mod列表，刷新UI
                await LoadModsListOnlyAsync();
                
                // 异步加载图标，不阻塞UI
                _ = LoadAllIconsAsync(_pageCancellationTokenSource?.Token ?? default);
                
                // 显示结果
                StatusMessage = $"{updatedCount}{"VersionManagerPage_VersionsUpdatedText".GetLocalized()}，{upToDateCount}{"VersionManagerPage_VersionsUpToDateText".GetLocalized()}";
                
                // 保存结果到属性，用于结果弹窗
                UpdateResults = $"{updatedCount}{"VersionManagerPage_VersionsUpdatedText".GetLocalized()}，{upToDateCount}{"VersionManagerPage_VersionsUpToDateText".GetLocalized()}";
                
                // 显示结果弹窗
                IsResultDialogVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新Mod失败: {ex.Message}");
                StatusMessage = $"更新Mod失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                
                // 完成下载
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }
        
        // Modrinth更新信息数据模型
        private class ModrinthUpdateInfo
        {
            public string name { get; set; }
            public string version_number { get; set; }
            public string changelog { get; set; }
            public List<ModrinthDependency> dependencies { get; set; }
            public List<string> game_versions { get; set; }
            public string version_type { get; set; }
            public List<string> loaders { get; set; }
            public bool featured { get; set; }
            public string status { get; set; }
            public string requested_status { get; set; }
            public string id { get; set; }
            public string project_id { get; set; }
            public string author_id { get; set; }
            public string date_published { get; set; }
            public int downloads { get; set; }
            public string changelog_url { get; set; }
            public List<ModrinthFile> files { get; set; }
        }
        
        // Modrinth依赖数据模型
        private class ModrinthDependency
        {
            public string version_id { get; set; }
            public string project_id { get; set; }
            public string file_name { get; set; }
        }
        
        // Modrinth文件数据模型
        private class ModrinthFile
        {
            public Dictionary<string, string> hashes { get; set; }
            public string url { get; set; }
            public string filename { get; set; }
            public bool primary { get; set; }
            public long size { get; set; }
        }
        
        /// <summary>
        /// 尝试通过 Modrinth 更新 Mod
        /// </summary>
        /// <returns>更新结果</returns>
        private async Task<ModUpdateResult> TryUpdateModsViaModrinthAsync(
            List<string> modHashes,
            Dictionary<string, string> modFilePathMap,
            string modLoader,
            string gameVersion,
            string modsPath)
        {
            var result = new ModUpdateResult();
            
            try
            {
                // 构建API请求
                var requestBody = new
                {
                    hashes = modHashes,
                    algorithm = "sha1",
                    loaders = new[] { modLoader },
                    game_versions = new[] { gameVersion }
                };
                
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 请求更新信息，Mod数量: {modHashes.Count}");
                
                // 调用Modrinth API
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", GetModrinthUserAgent());
                    
                    string apiUrl = TransformModrinthApiUrl("https://api.modrinth.com/v2/version_files/update");
                    var content = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json");
                    
                    var response = await httpClient.PostAsync(apiUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"[Modrinth] API响应成功");
                        
                        // 解析响应
                        var updateInfo = System.Text.Json.JsonSerializer.Deserialize<
                            System.Collections.Generic.Dictionary<string, ModrinthUpdateInfo>
                        >(responseContent);
                        
                        if (updateInfo != null && updateInfo.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Modrinth] 找到 {updateInfo.Count} 个Mod的更新信息");
                            
                            // 处理每个Mod的更新
                            foreach (var kvp in updateInfo)
                            {
                                string hash = kvp.Key;
                                ModrinthUpdateInfo info = kvp.Value;
                                
                                if (modFilePathMap.TryGetValue(hash, out string modFilePath))
                                {
                                    result.ProcessedMods.Add(modFilePath);
                                    
                                    // 检查是否需要更新
                                    bool needsUpdate = true;
                                    
                                    // 检查是否已有相同SHA1的Mod
                                    if (info.files != null && info.files.Count > 0)
                                    {
                                        var primaryFile = info.files.FirstOrDefault(f => f.primary) ?? info.files[0];
                                        if (primaryFile.hashes.TryGetValue("sha1", out string newSha1))
                                        {
                                            // 计算当前Mod的SHA1
                                            string currentSha1 = CalculateSHA1(modFilePath);
                                            if (currentSha1.Equals(newSha1, StringComparison.OrdinalIgnoreCase))
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[Modrinth] Mod {Path.GetFileName(modFilePath)} 已经是最新版本");
                                                needsUpdate = false;
                                                result.UpToDateCount++;
                                            }
                                        }
                                    }
                                    
                                    if (needsUpdate)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Modrinth] 正在更新Mod: {Path.GetFileName(modFilePath)}");
                                        
                                        // 获取主要文件
                                        var primaryFile = info.files.FirstOrDefault(f => f.primary) ?? info.files[0];
                                        if (!string.IsNullOrEmpty(primaryFile.url) && !string.IsNullOrEmpty(primaryFile.filename))
                                        {
                                            // 临时文件路径
                                            string tempFilePath = Path.Combine(modsPath, $"{primaryFile.filename}.tmp");
                                            // 最终文件路径
                                            string finalFilePath = Path.Combine(modsPath, primaryFile.filename);
                                            
                                            // 下载最新版本
                                            bool downloadSuccess = await DownloadModAsync(primaryFile.url, tempFilePath);
                                            if (downloadSuccess)
                                            {
                                                // 处理依赖关系
                                                if (info.dependencies != null && info.dependencies.Count > 0)
                                                {
                                                    // 转换依赖类型
                                                    var coreDependencies = info.dependencies.Select(dep => new Core.Models.Dependency
                                                    {
                                                        VersionId = dep.version_id,
                                                        ProjectId = dep.project_id,
                                                        FileName = dep.file_name
                                                    }).ToList();
                                                    await ProcessDependenciesAsync(coreDependencies, modsPath);
                                                }
                                                
                                                // 删除旧Mod文件
                                                if (File.Exists(modFilePath))
                                                {
                                                    File.Delete(modFilePath);
                                                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 已删除旧Mod文件: {modFilePath}");
                                                }
                                                
                                                // 重命名临时文件为最终文件名
                                                // 先检查目标文件是否已存在，如果存在则删除
                                                if (File.Exists(finalFilePath))
                                                {
                                                    File.Delete(finalFilePath);
                                                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 已删除已存在的目标文件: {finalFilePath}");
                                                }
                                                File.Move(tempFilePath, finalFilePath);
                                                System.Diagnostics.Debug.WriteLine($"[Modrinth] 已更新Mod: {finalFilePath}");
                                                
                                                result.UpdatedCount++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[Modrinth] 没有找到任何Mod的更新信息");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Modrinth] API调用失败: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 更新失败: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// 尝试通过 CurseForge 更新 Mod
        /// </summary>
        /// <returns>更新结果</returns>
        private async Task<ModUpdateResult> TryUpdateModsViaCurseForgeAsync(
            List<ModInfo> mods,
            string modLoader,
            string gameVersion,
            string modsPath)
        {
            var result = new ModUpdateResult();
            
            try
            {
                // 计算 CurseForge Fingerprint
                var fingerprintMap = new Dictionary<uint, string>(); // fingerprint -> filePath
                var fingerprints = new List<uint>();
                
                foreach (var mod in mods)
                {
                    try
                    {
                        uint fingerprint = Core.Helpers.CurseForgeFingerprintHelper.ComputeFingerprint(mod.FilePath);
                        fingerprints.Add(fingerprint);
                        fingerprintMap[fingerprint] = mod.FilePath;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] Mod {mod.Name} 的Fingerprint: {fingerprint}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 计算Fingerprint失败: {mod.Name}, 错误: {ex.Message}");
                    }
                }
                
                if (fingerprints.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 没有可查询的Fingerprint");
                    return result;
                }
                
                System.Diagnostics.Debug.WriteLine($"[CurseForge] 查询 {fingerprints.Count} 个Mod的Fingerprint");
                
                // 调用 CurseForge API
                var curseForgeService = App.GetService<Core.Services.CurseForgeService>();
                if (curseForgeService == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] CurseForgeService 未注册");
                    return result;
                }
                
                var matchResult = await curseForgeService.GetFingerprintMatchesAsync(fingerprints);
                
                if (matchResult.ExactMatches != null && matchResult.ExactMatches.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 找到 {matchResult.ExactMatches.Count} 个精确匹配");
                    
                    // 转换 ModLoader 类型到 CurseForge 的枚举值
                    int? modLoaderType = modLoader.ToLower() switch
                    {
                        "forge" => 1,
                        "fabric" => 4,
                        "quilt" => 5,
                        "neoforge" => 6,
                        _ => null
                    };
                    
                    foreach (var match in matchResult.ExactMatches)
                    {
                        if (match.File == null)
                            continue;
                        
                        // 查找对应的文件路径
                        uint matchedFingerprint = (uint)match.File.FileFingerprint;
                        if (!fingerprintMap.TryGetValue(matchedFingerprint, out string modFilePath))
                            continue;
                        
                        result.ProcessedMods.Add(modFilePath);
                        
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 处理Mod: {Path.GetFileName(modFilePath)}");
                        
                        // 获取最新的兼容文件
                        Core.Models.CurseForgeFile latestFile = null;
                        
                        if (match.LatestFiles != null && match.LatestFiles.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] LatestFiles 数量: {match.LatestFiles.Count}");
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 当前游戏版本: {gameVersion}, ModLoader: {modLoader}");
                            
                            // 输出所有可用文件的信息
                            for (int i = 0; i < match.LatestFiles.Count; i++)
                            {
                                var file = match.LatestFiles[i];
                                System.Diagnostics.Debug.WriteLine($"[CurseForge] 文件 {i + 1}: {file.FileName}");
                                System.Diagnostics.Debug.WriteLine($"[CurseForge]   - 支持的版本: {string.Join(", ", file.GameVersions ?? new List<string>())}");
                                System.Diagnostics.Debug.WriteLine($"[CurseForge]   - 文件日期: {file.FileDate}");
                            }
                            
                            // 筛选兼容的文件
                            var compatibleFiles = match.LatestFiles
                                .Where(f => f.GameVersions != null &&
                                           f.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                                .ToList();
                            
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 游戏版本兼容的文件数量: {compatibleFiles.Count}");
                            
                            // 如果指定了 ModLoader，进一步筛选
                            if (modLoaderType.HasValue)
                            {
                                var loaderCompatibleFiles = compatibleFiles
                                    .Where(f => f.GameVersions.Any(v => 
                                        v.Equals(modLoader, StringComparison.OrdinalIgnoreCase)))
                                    .ToList();
                                
                                System.Diagnostics.Debug.WriteLine($"[CurseForge] ModLoader 兼容的文件数量: {loaderCompatibleFiles.Count}");
                                
                                if (loaderCompatibleFiles.Count > 0)
                                {
                                    compatibleFiles = loaderCompatibleFiles;
                                }
                            }
                            
                            // 选择最新的文件
                            latestFile = compatibleFiles
                                .OrderByDescending(f => f.FileDate)
                                .FirstOrDefault();
                            
                            if (latestFile != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CurseForge] 选择的文件: {latestFile.FileName}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] LatestFiles 为空或数量为 0");
                        }
                        
                        if (latestFile == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 没有找到兼容的文件");
                            continue;
                        }
                        
                        // 检查是否需要更新
                        bool needsUpdate = match.File.Id != latestFile.Id;
                        
                        if (!needsUpdate)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] Mod {Path.GetFileName(modFilePath)} 已经是最新版本");
                            result.UpToDateCount++;
                            continue;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 正在更新Mod: {Path.GetFileName(modFilePath)}");
                        
                        // 下载最新版本
                        if (!string.IsNullOrEmpty(latestFile.DownloadUrl) && !string.IsNullOrEmpty(latestFile.FileName))
                        {
                            string tempFilePath = Path.Combine(modsPath, $"{latestFile.FileName}.tmp");
                            string finalFilePath = Path.Combine(modsPath, latestFile.FileName);
                            
                            bool downloadSuccess = await curseForgeService.DownloadFileAsync(
                                latestFile.DownloadUrl,
                                tempFilePath,
                                (fileName, progress) =>
                                {
                                    CurrentDownloadItem = fileName;
                                    DownloadProgress = progress;
                                });
                            
                            if (downloadSuccess)
                            {
                                // 处理依赖关系
                                if (latestFile.Dependencies != null && latestFile.Dependencies.Count > 0)
                                {
                                    await curseForgeService.ProcessDependenciesAsync(
                                        latestFile.Dependencies,
                                        modsPath,
                                        latestFile);
                                }
                                
                                // 删除旧Mod文件
                                if (File.Exists(modFilePath))
                                {
                                    File.Delete(modFilePath);
                                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 已删除旧Mod文件: {modFilePath}");
                                }
                                
                                // 重命名临时文件为最终文件名
                                if (File.Exists(finalFilePath))
                                {
                                    File.Delete(finalFilePath);
                                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 已删除已存在的目标文件: {finalFilePath}");
                                }
                                File.Move(tempFilePath, finalFilePath);
                                System.Diagnostics.Debug.WriteLine($"[CurseForge] 已更新Mod: {finalFilePath}");
                                
                                result.UpdatedCount++;
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 没有找到任何精确匹配");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge] 更新失败: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Mod 更新结果
        /// </summary>
        private class ModUpdateResult
        {
            public HashSet<string> ProcessedMods { get; set; } = new HashSet<string>();
            public int UpdatedCount { get; set; } = 0;
            public int UpToDateCount { get; set; } = 0;
        }
        
        /// <summary>
        /// 下载Mod文件
        /// </summary>
        /// <param name="downloadUrl">下载URL</param>
        /// <param name="destinationPath">保存路径</param>
        /// <returns>是否下载成功</returns>
        private async Task<bool> DownloadModAsync(string downloadUrl, string destinationPath)
        {
            try
            {
                string modName = Path.GetFileName(destinationPath);
                System.Diagnostics.Debug.WriteLine($"开始下载Mod: {downloadUrl} 到 {destinationPath}");
                
                // 更新当前下载项
                CurrentDownloadItem = modName;
                
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    // 创建父目录（如果不存在）
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? string.Empty);
                    
                    // 下载文件
                    var response = await httpClient.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    
                    long totalBytes = response.Content.Headers.ContentLength ?? 0;
                    long downloadedBytes = 0;
                    
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;
                            
                            // 计算并报告进度
                            if (totalBytes > 0)
                            {
                                double progress = (double)downloadedBytes / totalBytes * 100;
                                DownloadProgress = Math.Round(progress, 2);
                                System.Diagnostics.Debug.WriteLine($"下载进度: {DownloadProgress:F2}% ({downloadedBytes}/{totalBytes} bytes)");
                            }
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Mod下载完成: {destinationPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"下载Mod失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 获取Mod版本信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <returns>版本信息</returns>
        private async Task<ModrinthUpdateInfo> GetModrinthVersionInfoAsync(string versionId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"获取Mod版本信息: {versionId}");
                
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", GetModrinthUserAgent());
                    
                    string apiUrl = TransformModrinthApiUrl($"https://api.modrinth.com/v2/version/{versionId}");
                    var response = await httpClient.GetAsync(apiUrl);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"版本信息响应: {responseContent}");
                        
                        return System.Text.Json.JsonSerializer.Deserialize<ModrinthUpdateInfo>(responseContent);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"获取版本信息失败: {response.StatusCode}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取版本信息失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 处理Mod依赖关系
        /// </summary>
        /// <param name="dependencies">依赖列表</param>
        /// <param name="modsPath">Mod保存路径</param>
        /// <returns>成功处理的依赖数量</returns>
        private async Task<int> ProcessDependenciesAsync(List<Core.Models.Dependency> dependencies, string modsPath)
        {
            if (dependencies == null || dependencies.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("没有依赖需要处理");
                return 0;
            }
            
            try
            {
                // 获取ModrinthService实例
                var modrinthService = App.GetService<Core.Services.ModrinthService>();
                
                // 获取当前版本的ModLoader和游戏版本
                string modLoader = "fabric"; // 默认fabric
                string gameVersion = SelectedVersion?.VersionNumber ?? "1.19.2"; // 使用选中版本的VersionNumber
                
                // 使用VersionInfoService获取完整的版本配置信息
                var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                if (versionInfoService != null && SelectedVersion != null)
                {
                    string versionDir = Path.Combine(SelectedVersion.Path);
                    Core.Models.VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(SelectedVersion.Name, versionDir);
                    
                    if (versionConfig != null)
                    {
                        // 获取ModLoader类型
                        if (!string.IsNullOrEmpty(versionConfig.ModLoaderType))
                        {
                            modLoader = versionConfig.ModLoaderType.ToLower();
                        }
                        else
                        {
                            // 回退到基于版本名的判断
                            if (SelectedVersion.Name.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "fabric";
                            }
                            else if (SelectedVersion.Name.Contains("forge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "forge";
                            }
                            else if (SelectedVersion.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "neoforge";
                            }
                            else if (SelectedVersion.Name.Contains("quilt", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "quilt";
                            }
                        }
                        
                        // 获取游戏版本
                        if (!string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                        {
                            gameVersion = versionConfig.MinecraftVersion;
                        }
                    }
                }
                
                // 创建当前Mod版本信息对象，用于筛选兼容的依赖版本
                var currentModVersion = new Core.Models.ModrinthVersion
                {
                    Loaders = new List<string> { modLoader },
                    GameVersions = new List<string> { gameVersion }
                };
                
                // 直接使用ModrinthService处理依赖，不需要转换类型
                return await modrinthService.ProcessDependenciesAsync(
                    dependencies,
                    modsPath,
                    currentModVersion, // 传递当前版本信息，用于筛选兼容的依赖版本
                    (modName, progress) => {
                        // 更新下载状态
                        CurrentDownloadItem = modName;
                        DownloadProgress = progress;
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理依赖失败: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// 仅加载mod列表，不加载图标
        /// </summary>
        private async Task LoadModsListOnlyAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }
            
            var modsPath = GetVersionSpecificPath("mods");
            
            // IO操作移至后台线程，避免阻塞UI导致加载动画卡顿
            var newModsList = await Task.Run(() => 
            {
                var result = new List<ModInfo>();
                try
                {
                    if (Directory.Exists(modsPath))
                    {
                        // 获取所有mod文件（.jar和.jar.disabled）
                        var modFiles = Directory
                            .GetFiles(modsPath, "*.jar*")
                            .Where(modFile => modFile.EndsWith(".jar") || modFile.EndsWith(".jar.disabled"));
                    
                        // 遍历所有mod文件，创建mod信息对象
                        foreach (var modFile in modFiles)
                        {
                            var modInfo = new ModInfo(modFile);
                            // 先设置默认图标为空，后续异步加载
                            modInfo.Icon = null;
                            result.Add(modInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadModsListOnlyAsync] Error: {ex.Message}");
                }
                return result;
            });
            
            // 回到UI线程更新列表数据源
            _allMods = newModsList;
            
            // 如果页面动画已经播放完毕（_isPageReady == true），则立即刷新
            // 否则（_isPageReady == false），等待主流程中的 RefreshAllCollections 调用
            if (_isPageReady)
            {
                 App.MainWindow.DispatcherQueue.TryEnqueue(FilterMods);
            }
        }
        
        /// <summary>
        /// 异步加载所有 Mod 的描述信息
        /// </summary>
        private async Task LoadAllModDescriptionsAsync(ObservableCollection<ModInfo> mods)
        {
            var cancellationToken = _pageCancellationTokenSource?.Token ?? CancellationToken.None;
            
            foreach (var mod in mods.ToList())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                
                _ = LoadModDescriptionAsync(mod, cancellationToken);
                
                // 稍微延迟，避免同时发起太多请求
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
        
        /// <summary>
        /// 加载单个 Mod 的描述信息
        /// </summary>
        private async Task LoadModDescriptionAsync(ModInfo mod, CancellationToken cancellationToken)
        {
            try
            {
                // 在 UI 线程设置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    mod.IsLoadingDescription = true;
                });
                
                // 在后台线程获取数据
                var metadata = await _modInfoService.GetModInfoAsync(mod.FilePath, cancellationToken);
                
                // 在 UI 线程更新属性
                if (metadata != null)
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        mod.Description = metadata.Description;
                        mod.Source = metadata.Source;
                        mod.ProjectId = metadata.ProjectId;
                        // 如果是 CurseForge，尝试获取 Project ID (这里 Source 可能是 "CurseForge")
                        if (mod.Source == "CurseForge" && metadata.CurseForgeModId > 0)
                        {
                            mod.ProjectId = metadata.CurseForgeModId.ToString();
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // 取消操作，忽略
            }
            catch
            {
                // 静默失败
            }
            finally
            {
                // 在 UI 线程重置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    mod.IsLoadingDescription = false;
                });
            }
        }
        
        /// <summary>
        /// 加载单个资源包的描述信息
        /// </summary>
        private async Task LoadResourcePackDescriptionAsync(ResourcePackInfo resourcePack, CancellationToken cancellationToken)
        {
            try
            {
                // 在 UI 线程设置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    resourcePack.IsLoadingDescription = true;
                });
                
                // 在后台线程获取数据（使用与 Mod 相同的服务）
                var metadata = await _modInfoService.GetModInfoAsync(resourcePack.FilePath, cancellationToken);
                
                // 在 UI 线程更新属性
                if (metadata != null)
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        resourcePack.Description = metadata.Description;
                        resourcePack.Source = metadata.Source;
                        resourcePack.ProjectId = metadata.ProjectId;
                        
                        if (resourcePack.Source == "CurseForge" && metadata.CurseForgeModId > 0)
                        {
                            resourcePack.ProjectId = metadata.CurseForgeModId.ToString();
                        }
                    });
                }
                else
                {
                    // 网络获取失败，尝试从 pack.mcmeta 读取
                    var localDescription = await ExtractPackMetaDescriptionAsync(resourcePack.FilePath);
                    if (!string.IsNullOrEmpty(localDescription))
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            resourcePack.Description = localDescription;
                            resourcePack.Source = "本地";
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 取消操作，忽略
            }
            catch
            {
                // 静默失败，尝试从本地读取
                try
                {
                    var localDescription = await ExtractPackMetaDescriptionAsync(resourcePack.FilePath);
                    if (!string.IsNullOrEmpty(localDescription))
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            resourcePack.Description = localDescription;
                            resourcePack.Source = "本地";
                        });
                    }
                }
                catch { }
            }
            finally
            {
                // 在 UI 线程重置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    resourcePack.IsLoadingDescription = false;
                });
            }
        }
        
        /// <summary>
        /// 从资源包的 pack.mcmeta 文件中提取描述
        /// </summary>
        private async Task<string> ExtractPackMetaDescriptionAsync(string resourcePackPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(resourcePackPath))
                        return null;
                    
                    using var archive = System.IO.Compression.ZipFile.OpenRead(resourcePackPath);
                    var metaEntry = archive.GetEntry("pack.mcmeta");
                    
                    if (metaEntry == null)
                        return null;
                    
                    using var stream = metaEntry.Open();
                    using var reader = new StreamReader(stream);
                    var jsonContent = reader.ReadToEnd();
                    
                    // 解析 JSON
                    var jsonDoc = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);
                    var packNode = jsonDoc["pack"];
                    
                    if (packNode == null)
                        return null;
                    
                    var descriptionNode = packNode["description"];
                    
                    if (descriptionNode == null)
                        return null;
                    
                    // 处理两种格式
                    if (descriptionNode.Type == Newtonsoft.Json.Linq.JTokenType.String)
                    {
                        // 简单字符串格式
                        return descriptionNode.ToString();
                    }
                    else if (descriptionNode.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                    {
                        // 复杂格式：数组对象
                        var textParts = new List<string>();
                        foreach (var item in descriptionNode)
                        {
                            var text = item["text"]?.ToString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                textParts.Add(text);
                            }
                        }
                        return string.Join("", textParts);
                    }
                    
                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"提取 pack.mcmeta 描述失败: {ex.Message}");
                    return null;
                }
            });
        }
        
        /// <summary>
        /// 加载单个光影的描述信息
        /// </summary>
        private async Task LoadShaderDescriptionAsync(ShaderInfo shader, CancellationToken cancellationToken)
        {
            try
            {
                // 在 UI 线程设置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    shader.IsLoadingDescription = true;
                });
                
                // 在后台线程获取数据（使用与 Mod 相同的服务）
                var metadata = await _modInfoService.GetModInfoAsync(shader.FilePath, cancellationToken);
                
                // 在 UI 线程更新属性
                if (metadata != null)
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        shader.Description = metadata.Description;
                        shader.Source = metadata.Source;
                        shader.ProjectId = metadata.ProjectId;

                        if (shader.Source == "CurseForge" && metadata.CurseForgeModId > 0)
                        {
                            shader.ProjectId = metadata.CurseForgeModId.ToString();
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // 取消操作，忽略
            }
            catch
            {
                // 静默失败
            }
            finally
            {
                // 在 UI 线程重置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    shader.IsLoadingDescription = false;
                });
            }
        }
        
        /// <summary>
        /// 加载mod列表
        /// </summary>
        private async Task LoadModsAsync()
        {
            await LoadModsListOnlyAsync();
            
            // 异步加载所有mod的图标，不阻塞UI
            var iconTasks = new List<Task>();
            foreach (var modInfo in Mods)
            {
                iconTasks.Add(LoadResourceIconAsync(icon => modInfo.Icon = icon, modInfo.FilePath, "mod", true));
            }
            
            // 并行执行图标加载任务
            await Task.WhenAll(iconTasks);
        }

    /// <summary>
    /// 打开mod文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenModFolderAsync()
    {
        await OpenFolderByTypeAsync("mods");
    }
    
    /// <summary>
        /// 切换mod启用状态
        /// </summary>
        /// <param name="mod">要切换状态的mod</param>
        /// <param name="isOn">开关的新状态</param>
        public async Task ToggleModEnabledAsync(ModInfo mod, bool isOn)
        {
            if (mod == null)
            {
                return;
            }
            
            try
            {
                // 构建新的文件名和路径
                string newFileName;
                string newFilePath;
                string oldFilePath = mod.FilePath;
                
                // 直接基于isOn值决定新的状态，而不是mod.IsEnabled
                if (isOn)
                {
                    // 启用状态：确保文件名没有.disabled后缀
                    if (mod.FileName.EndsWith(".disabled"))
                    {
                        newFileName = mod.FileName.Substring(0, mod.FileName.Length - ".disabled".Length);
                        newFilePath = Path.Combine(Path.GetDirectoryName(mod.FilePath), newFileName);
                    }
                    else
                    {
                        // 已经是启用状态，无需操作
                        return;
                    }
                }
                else
                {
                    // 禁用状态：添加.disabled后缀
                    newFileName = mod.FileName + ".disabled";
                    newFilePath = Path.Combine(Path.GetDirectoryName(mod.FilePath), newFileName);
                }
                
                // 重命名文件
                if (File.Exists(oldFilePath))
                {
                    // 执行文件重命名
                    File.Move(oldFilePath, newFilePath);
                    
                    // 更新mod信息，确保状态一致性
                    mod.IsEnabled = isOn;
                    mod.FileName = newFileName;
                    mod.FilePath = newFilePath; // 更新FilePath，确保下次操作能找到正确的文件
                    
                    StatusMessage = $"已{(isOn ? "启用" : "禁用")}mod: {mod.Name}";
                }
            }
            catch (Exception ex)
            {
                // 恢复状态，确保UI与实际文件状态一致
                // 重新从文件名判断实际状态
                mod.IsEnabled = !mod.FileName.EndsWith(".disabled");
                StatusMessage = $"切换mod状态失败：{ex.Message}";
            }
        }
        
        /// <summary>
        /// 删除mod命令
        /// </summary>
        /// <param name="mod">要删除的mod</param>
        [RelayCommand]
        private async Task DeleteModAsync(ModInfo mod)
        {
            if (mod == null)
            {
                return;
            }
            
            try
            {
                // 删除文件
                if (File.Exists(mod.FilePath))
                {
                    File.Delete(mod.FilePath);
                }
                
                // 从列表中移除
                Mods.Remove(mod);
                
                StatusMessage = $"已删除mod: {mod.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除mod失败：{ex.Message}";
            }
        }
    
    /// <summary>
    /// 导航到Mod页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToModPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为1（Mod下载标签页）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 1;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }
    
    /// <summary>
    /// 导航到光影页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToShaderPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为2（光影下载标签页）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 2;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }
    
    /// <summary>
    /// 导航到资源包页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToResourcePackPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为3（资源包下载标签页）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 3;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }
    
    /// <summary>
    /// 导航到数据包页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToDataPackPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为3（资源包下载标签页，数据包和资源包共用一个页面）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 3;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }
    
    /// <summary>
    /// 导航到地图页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToMapPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为6（世界下载标签页）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 6;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }

    #endregion
}
