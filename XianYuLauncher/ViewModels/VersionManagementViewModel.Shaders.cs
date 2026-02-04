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
using Microsoft.UI.Xaml;
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
    private async Task NavigateToShaderDetails(ShaderInfo shader)
    {
        if (shader == null) return;

        if (string.IsNullOrEmpty(shader.ProjectId))
        {
            shader.IsLoadingDescription = true;
            try
            {
               await LoadShaderDescriptionAsync(shader, default);
            }
            finally
            {
               shader.IsLoadingDescription = false;
            }
        }

        if (!string.IsNullOrEmpty(shader.ProjectId))
        {
            string navigationId = shader.ProjectId;
            if (shader.Source == "CurseForge" && !navigationId.StartsWith("curseforge-"))
            {
                navigationId = "curseforge-" + navigationId;
            }
            
            _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName, navigationId);
        }
        else
        {
             StatusMessage = "无法获取该光影的详细信息";
        }
    }

    #region 光影管理

    /// <summary>
        /// 仅加载光影列表，不加载图标
        /// </summary>
        private async Task LoadShadersListOnlyAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }

            var shadersPath = GetVersionSpecificPath("shaderpacks");
            if (Directory.Exists(shadersPath))
            {
                // 获取所有光影文件夹和zip文件
                var shaderFolders = Directory.GetDirectories(shadersPath);
                var shaderZips = Directory.GetFiles(shadersPath, "*.zip");
                
                // 创建新的光影列表，减少CollectionChanged事件触发次数
                var newShaders = new ObservableCollection<ShaderInfo>();
                
                // 添加所有光影文件夹
                foreach (var shaderFolder in shaderFolders)
                {
                    var shaderInfo = new ShaderInfo(shaderFolder);
                    // 先设置默认图标为空，后续异步加载
                    shaderInfo.Icon = null;
                    newShaders.Add(shaderInfo);
                }
                
                // 添加所有光影zip文件
                foreach (var shaderZip in shaderZips)
                {
                    var shaderInfo = new ShaderInfo(shaderZip);
                    // 先设置默认图标为空，后续异步加载
                    shaderInfo.Icon = null;
                    newShaders.Add(shaderInfo);
                }
                
                // 立即显示光影列表，不等待图标加载完成
                _allShaders = newShaders.ToList();
                FilterShaders();
            }
            else
            {
                // 清空光影列表
                _allShaders.Clear();
                FilterShaders();
            }
        }

    private async Task LoadShadersAsync() => await LoadShadersListOnlyAsync();

    private async Task OpenShaderFolderAsync()
    {
        if (SelectedVersion == null) return;
        string path = GetVersionSpecificPath("shaderpacks");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        await Launcher.LaunchUriAsync(new Uri(path));
    }

    [ObservableProperty]
    private bool _isShaderSelectionModeEnabled;

    [RelayCommand]
    private void ToggleShaderSelectionMode()
    {
        IsShaderSelectionModeEnabled = !IsShaderSelectionModeEnabled;
        if (!IsShaderSelectionModeEnabled)
        {
            foreach (var shader in Shaders)
            {
                shader.IsSelected = false;
            }
        }
    }

    [RelayCommand]
    private void SelectAllShaders()
    {
        if (Shaders.Count == 0) return;
        bool allSelected = Shaders.All(s => s.IsSelected);
        foreach (var shader in Shaders)
        {
            shader.IsSelected = !allSelected;
        }
    }

    private List<ShaderInfo> _selectedShadersForMove;

    [RelayCommand]
    private async Task MoveShadersToOtherVersionAsync()
    {
        var selectedShaders = Shaders.Where(s => s.IsSelected).ToList();
        if (selectedShaders.Count == 0)
        {
            StatusMessage = "请先选择要转移的光影";
            return;
        }

        _selectedShadersForMove = selectedShaders;
        await LoadTargetVersionsAsync();
        CurrentResourceMoveType = ResourceMoveType.Shader;
        IsMoveResourcesDialogVisible = true;
    }

    private async Task ConfirmMoveShadersAsync()
    {
        if (SelectedTargetVersion == null || _selectedShadersForMove == null || _selectedShadersForMove.Count == 0)
        {
            StatusMessage = "请选择要转移的光影和目标版本";
            return;
        }

        try
        {
            IsDownloading = true;
            DownloadProgressDialogTitle = "正在转移光影";
            DownloadProgress = 0;
            StatusMessage = "正在准备光影转移...";

            var originalSelectedVersion = SelectedVersion;
            string targetVersion = SelectedTargetVersion.VersionName;

            // 临时切换SelectedVersion以解析目标路径
            var targetVersionInfo = new VersionListViewModel.VersionInfoItem
            {
                Name = targetVersion,
                Path = Path.Combine(_fileService.GetMinecraftDataPath(), "versions", targetVersion)
            };
            
            if (!Directory.Exists(targetVersionInfo.Path))
            {
                 throw new Exception($"无法找到目标版本: {targetVersion}");
            }

            // 手动获取目标版本的路径，不依赖 SelectedVersion 属性 setter (以防触发副作用)
            // 但 GetVersionSpecificPath 依赖 SelectedVersion。
            // 简单起见，我们暂时设置 SelectedVersion，操作完还原
            SelectedVersion = targetVersionInfo;
            string targetVersionPath = GetVersionSpecificPath("shaderpacks");
            Directory.CreateDirectory(targetVersionPath);
            // 还原
            SelectedVersion = originalSelectedVersion;


            var moveResults = new List<MoveModResult>();

            for (int i = 0; i < _selectedShadersForMove.Count; i++)
            {
                var shader = _selectedShadersForMove[i];
                var result = new MoveModResult
                {
                    ModName = shader.Name,
                    SourcePath = shader.FilePath,
                    Status = MoveModStatus.Failed
                };

                try
                {
                    string destPath = Path.Combine(targetVersionPath, Path.GetFileName(shader.FilePath));
                    
                    if (Directory.Exists(shader.FilePath))
                    {
                        CopyDirectory(shader.FilePath, destPath);
                        result.Status = MoveModStatus.Copied;
                        result.TargetPath = destPath;
                    }
                    else if (File.Exists(shader.FilePath))
                    {
                        File.Copy(shader.FilePath, destPath, true);
                        result.Status = MoveModStatus.Copied;
                        result.TargetPath = destPath;
                    }
                    else
                    {
                        result.ErrorMessage = "源文件不存在";
                    }
                }
                catch (Exception ex)
                {
                    result.Status = MoveModStatus.Failed;
                    result.ErrorMessage = ex.Message;
                }

                moveResults.Add(result);
                DownloadProgress = (i + 1) / (double)_selectedShadersForMove.Count * 100;
            }

            MoveResults = moveResults;
            IsMoveResultDialogVisible = true;
            
            // 重新加载当前列表
            await LoadShadersListOnlyAsync();
            StatusMessage = $"光影转移完成";
        }
        catch (Exception ex)
        {
             StatusMessage = $"光影转移失败: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            IsMoveResourcesDialogVisible = false;
        }
    }

    private void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    [RelayCommand]
    private async Task DeleteShader(ShaderInfo shader)
    {
        if (shader == null) return;

        var dialog = new ContentDialog
        {
            Title = "删除光影",
            Content = $"确定要删除光影 \"{shader.Name}\" 吗？此操作无法撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            if (Directory.Exists(shader.FilePath))
            {
                Directory.Delete(shader.FilePath, true);
            }
            else if (File.Exists(shader.FilePath))
            {
                File.Delete(shader.FilePath);
            }
            
            // 删除同名配置文件（如果存在）
            string configFilePath = $"{shader.FilePath}.txt";
            if (File.Exists(configFilePath))
            {
                File.Delete(configFilePath);
            }
            
            // 从列表中移除
            Shaders.Remove(shader);
            
            StatusMessage = $"已删除光影: {shader.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除光影失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UpdateShadersAsync()
    {
         try
            {
                // 获取选中的Shaders
                var selectedShaders = Shaders.Where(s => s.IsSelected).ToList();
                if (selectedShaders.Count == 0)
                {
                    StatusMessage = "请先选择要更新的光影";
                    return;
                }
                
                // 设置下载状态
                IsDownloading = true;
                DownloadProgressDialogTitle = "正在更新光影...";
                DownloadProgress = 0;
                CurrentDownloadItem = string.Empty;
                
                // 计算选中Mod的SHA1哈希值（用于Modrinth）
                var shaderHashes = new List<string>();
                var shaderFilePathMap = new Dictionary<string, string>();
                
                foreach (var shader in selectedShaders)
                {
                    // 跳过文件夹类型的光影，目前只支持zip文件更新
                    if (Directory.Exists(shader.FilePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"跳过文件夹光影: {shader.Name}");
                        continue;
                    }

                    try
                    {
                        string sha1Hash = CalculateSHA1(shader.FilePath);
                        shaderHashes.Add(sha1Hash);
                        shaderFilePathMap[sha1Hash] = shader.FilePath;
                    }
                    catch (Exception ex)
                    {
                    }
                }
                
                if (shaderHashes.Count == 0)
                {
                    StatusMessage = "没有可更新的光影文件（仅支持.zip文件更新）";
                    IsDownloading = false;
                    return;
                }

                // 获取游戏版本
                string gameVersion = SelectedVersion?.VersionNumber ?? "1.19.2"; 
                 var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                if (versionInfoService != null && SelectedVersion != null)
                {
                    string versionDir = Path.Combine(SelectedVersion.Path);
                    Core.Models.VersionConfig versionConfig = versionInfoService.GetFullVersionInfo(SelectedVersion.Name, versionDir);
                    if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                    {
                        gameVersion = versionConfig.MinecraftVersion;
                    }
                }
                
                // 获取Shaders文件夹路径
                string shadersPath = GetVersionSpecificPath("shaderpacks");
                
                int updatedCount = 0;
                int upToDateCount = 0;
                
                // 第一步：尝试通过 Modrinth 更新
                var modrinthResult = await TryUpdateShadersViaModrinthAsync(
                    shaderHashes, 
                    shaderFilePathMap, 
                    gameVersion, 
                    shadersPath);
                
                updatedCount += modrinthResult.UpdatedCount;
                upToDateCount += modrinthResult.UpToDateCount;
                
                // 第二步：对于 Modrinth 未找到的 光影，尝试通过 CurseForge 更新
                var failedShaders = selectedShaders
                    .Where(s => !Directory.Exists(s.FilePath) && !modrinthResult.ProcessedMods.Contains(s.FilePath))
                    .ToList();
                
                if (failedShaders.Count > 0)
                {
                     var curseForgeResult = await TryUpdateShadersViaCurseForgeAsync(
                         failedShaders,
                         gameVersion,
                         shadersPath);
                     
                     updatedCount += curseForgeResult.UpdatedCount;
                     upToDateCount += curseForgeResult.UpToDateCount;
                }
                
                // 重新加载列表
                await LoadShadersListOnlyAsync();
                
                // 显示结果
                StatusMessage = $"{updatedCount} 个光影已更新，{upToDateCount} 个光影已是最新";
                UpdateResults = StatusMessage;
                IsResultDialogVisible = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"更新光影失败: {ex.Message}";
                IsResultDialogVisible = true;
                UpdateResults = $"更新失败: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
    }

    private async Task<ModUpdateResult> TryUpdateShadersViaModrinthAsync(
        List<string> hashes,
        Dictionary<string, string> filePathMap,
        string gameVersion,
        string savePath)
    {
        var result = new ModUpdateResult();
        try
        {
             // 构建API请求
             // 光影一般兼容 iris, optifine, 或者 minecraft 本身
            var requestBody = new
            {
                hashes = hashes,
                algorithm = "sha1",
                loaders = new[] { "iris", "optifine", "minecraft" },
                game_versions = new[] { gameVersion }
            };
            
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
                    var updateInfo = System.Text.Json.JsonSerializer.Deserialize<
                        System.Collections.Generic.Dictionary<string, ModrinthUpdateInfo>
                    >(responseContent);
                    
                    if (updateInfo != null && updateInfo.Count > 0)
                    {
                        foreach (var kvp in updateInfo)
                        {
                            string hash = kvp.Key;
                            ModrinthUpdateInfo info = kvp.Value;
                            
                            if (filePathMap.TryGetValue(hash, out string filePath))
                            {
                                result.ProcessedMods.Add(filePath);
                                bool needsUpdate = true;
                                if (info.files != null && info.files.Count > 0)
                                {
                                    var primaryFile = info.files.FirstOrDefault(f => f.primary) ?? info.files[0];
                                    if (primaryFile.hashes.TryGetValue("sha1", out string newSha1))
                                    {
                                        string currentSha1 = CalculateSHA1(filePath);
                                        if (currentSha1.Equals(newSha1, StringComparison.OrdinalIgnoreCase))
                                        {
                                            needsUpdate = false;
                                            result.UpToDateCount++;
                                        }
                                    }
                                }
                                
                                if (needsUpdate)
                                {
                                    var primaryFile = info.files.FirstOrDefault(f => f.primary) ?? info.files[0];
                                    if (!string.IsNullOrEmpty(primaryFile.url) && !string.IsNullOrEmpty(primaryFile.filename))
                                    {
                                        string tempFilePath = Path.Combine(savePath, $"{primaryFile.filename}.tmp");
                                        string finalFilePath = Path.Combine(savePath, primaryFile.filename);
                                        
                                        bool downloadSuccess = await DownloadModAsync(primaryFile.url, tempFilePath);
                                        if (downloadSuccess)
                                        {
                                             // 光影一般没有复杂的依赖，直接处理
                                            if (File.Exists(filePath))
                                            {
                                                File.Delete(filePath);
                                                // 对应的 config txt
                                                string txtConfig = $"{filePath}.txt";
                                                if (File.Exists(txtConfig)) File.Delete(txtConfig);
                                            }
                                            if (File.Exists(finalFilePath)) File.Delete(finalFilePath);
                                            File.Move(tempFilePath, finalFilePath);
                                            result.UpdatedCount++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth] 光影更新检查失败: {ex.Message}");
        }
        return result;
    }

    private async Task<ModUpdateResult> TryUpdateShadersViaCurseForgeAsync(
        List<ShaderInfo> shaders,
        string gameVersion,
        string savePath)
    {
        var result = new ModUpdateResult();
        try
        {
            var fingerprintMap = new Dictionary<uint, string>();
            var fingerprints = new List<uint>();
            
            foreach (var shader in shaders)
            {
                 // 文件夹无法计算指纹
                 if(Directory.Exists(shader.FilePath)) continue;
                 
                 try 
                 {
                    uint fp = Core.Helpers.CurseForgeFingerprintHelper.ComputeFingerprint(shader.FilePath);
                    fingerprints.Add(fp);
                    fingerprintMap[fp] = shader.FilePath;
                 }
                 catch {}
            }
            
            if (fingerprints.Count == 0) return result;

            var curseForgeService = App.GetService<Core.Services.CurseForgeService>();
            if (curseForgeService == null) return result;

            var matchResult = await curseForgeService.GetFingerprintMatchesAsync(fingerprints);
            if (matchResult.ExactMatches != null)
            {
                foreach (var match in matchResult.ExactMatches)
                {
                    if (match.File == null) continue;
                    uint matchedFp = (uint)match.File.FileFingerprint;
                    if (!fingerprintMap.TryGetValue(matchedFp, out string filePath)) continue;
                    
                    result.ProcessedMods.Add(filePath);
                    
                    if (match.LatestFiles != null && match.LatestFiles.Count > 0)
                    {
                        var compatibleFiles = match.LatestFiles
                            .Where(f => f.GameVersions != null && f.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                            .ToList();
                            
                        // 光影不筛选ModLoader，或尝试筛选 optifine/iris 如果API有提供，但CF通常不区分那么细
                        // 取最新文件
                        var latestFile = compatibleFiles.OrderByDescending(f => f.FileDate).FirstOrDefault();
                        if (latestFile != null)
                        {
                             if (latestFile.FileFingerprint != matchedFp)
                             {
                                 // 需要更新
                                 string tempFilePath = Path.Combine(savePath, $"{latestFile.FileName}.tmp");
                                 string finalFilePath = Path.Combine(savePath, latestFile.FileName);
                                 
                                 bool downloadSuccess = await DownloadModAsync(latestFile.DownloadUrl, tempFilePath);
                                 if (downloadSuccess)
                                 {
                                     if (File.Exists(filePath)) 
                                     {
                                         File.Delete(filePath);
                                         string txtConfig = $"{filePath}.txt";
                                         if (File.Exists(txtConfig)) File.Delete(txtConfig);
                                     }
                                     if (File.Exists(finalFilePath)) File.Delete(finalFilePath);
                                     File.Move(tempFilePath, finalFilePath);
                                     result.UpdatedCount++;
                                 }
                             }
                             else
                             {
                                 result.UpToDateCount++;
                             }
                        }
                    }
                }
            }
        }
        catch(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForge] 光影更新检查失败: {ex.Message}");
        }
        return result;
    }

    #endregion
}
