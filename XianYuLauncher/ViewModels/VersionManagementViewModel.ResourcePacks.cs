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
    private async Task NavigateToResourcePackDetails(ResourcePackInfo resourcePack)
    {
        if (resourcePack == null) return;

        if (string.IsNullOrEmpty(resourcePack.ProjectId))
        {
            resourcePack.IsLoadingDescription = true;
            try
            {
               await LoadResourcePackDescriptionAsync(resourcePack, default);
            }
            finally
            {
               resourcePack.IsLoadingDescription = false;
            }
        }

        if (!string.IsNullOrEmpty(resourcePack.ProjectId))
        {
            string navigationId = resourcePack.ProjectId;
            if (resourcePack.Source == "CurseForge" && !navigationId.StartsWith("curseforge-"))
            {
                navigationId = "curseforge-" + navigationId;
            }
            
            _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName, navigationId);
        }
        else
        {
             StatusMessage = "无法获取该资源包的详细信息";
        }
    }

    #region 资源包管理

    /// <summary>
        /// 仅加载资源包列表，不加载图标
        /// </summary>
        private async Task LoadResourcePacksListOnlyAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }

            var resourcePacksPath = GetVersionSpecificPath("resourcepacks");
            
            var newPackList = await Task.Run(() =>
            {
                var list = new List<ResourcePackInfo>();
                try
                {
                    if (Directory.Exists(resourcePacksPath))
                    {
                        // 获取所有资源包文件夹和zip文件
                        var resourcePackFolders = Directory.GetDirectories(resourcePacksPath);
                        var resourcePackZips = Directory.GetFiles(resourcePacksPath, "*.zip");
                        
                        // 添加所有资源包文件夹
                        foreach (var resourcePackFolder in resourcePackFolders)
                        {
                            var resourcePackInfo = new ResourcePackInfo(resourcePackFolder);
                            // 先设置默认图标为空，后续异步加载
                            resourcePackInfo.Icon = null;
                            list.Add(resourcePackInfo);
                        }
                        
                        // 添加所有资源包zip文件
                        foreach (var resourcePackZip in resourcePackZips)
                        {
                            var resourcePackInfo = new ResourcePackInfo(resourcePackZip);
                            // 先设置默认图标为空，后续异步加载
                            resourcePackInfo.Icon = null;
                            list.Add(resourcePackInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading resource packs: {ex.Message}");
                }
                return list;
            });

            _allResourcePacks = newPackList;

            if (_isPageReady)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(FilterResourcePacks);
            }
        }

    private async Task LoadResourcePacksAsync() => await LoadResourcePacksListOnlyAsync();

    private async Task OpenResourcePackFolderAsync()
    {
        if (SelectedVersion == null) return;
        string path = GetVersionSpecificPath("resourcepacks");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        await Launcher.LaunchUriAsync(new Uri(path));
    }

    [ObservableProperty]
    private bool _isResourcePackSelectionModeEnabled;

    [RelayCommand]
    private void ToggleResourcePackSelectionMode()
    {
        IsResourcePackSelectionModeEnabled = !IsResourcePackSelectionModeEnabled;
        if (!IsResourcePackSelectionModeEnabled)
        {
            foreach (var rp in ResourcePacks) rp.IsSelected = false;
        }
    }

    [RelayCommand]
    private void SelectAllResourcePacks()
    {
        if (ResourcePacks.Count == 0) return;
        bool allSelected = ResourcePacks.All(rp => rp.IsSelected);
        foreach (var rp in ResourcePacks) rp.IsSelected = !allSelected;
    }

    private List<ResourcePackInfo> _selectedResourcePacksForMove;

    [RelayCommand]
    private async Task MoveResourcePacksToOtherVersionAsync()
    {
        var selected = ResourcePacks.Where(rp => rp.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "请先选择要转移的资源包";
            return;
        }

        _selectedResourcePacksForMove = selected;
        await LoadTargetVersionsAsync();
        CurrentResourceMoveType = ResourceMoveType.ResourcePack;
        IsMoveResourcesDialogVisible = true;
    }

    private async Task ConfirmMoveResourcePacksAsync()
    {
        if (SelectedTargetVersion == null || _selectedResourcePacksForMove == null || _selectedResourcePacksForMove.Count == 0)
        {
            StatusMessage = "请选择要转移的资源包和目标版本";
            return;
        }

        try
        {
            IsDownloading = true;
            DownloadProgressDialogTitle = "正在转移资源包";
            DownloadProgress = 0;
            StatusMessage = "正在准备资源包转移...";

            string targetVersion = SelectedTargetVersion.VersionName;
            string targetBaseDir = Path.Combine(_fileService.GetMinecraftDataPath(), "versions", targetVersion);
            
            if (!Directory.Exists(targetBaseDir))
            {
                 throw new Exception($"无法找到目标版本: {targetVersion}");
            }

            string targetDir = Path.Combine(targetBaseDir, "resourcepacks");
            Directory.CreateDirectory(targetDir);

            var moveResults = new List<MoveModResult>();

            for (int i = 0; i < _selectedResourcePacksForMove.Count; i++)
            {
                var rp = _selectedResourcePacksForMove[i];
                var result = new MoveModResult
                {
                    ModName = rp.Name,
                    SourcePath = rp.FilePath,
                    Status = MoveModStatus.Failed
                };

                try
                {
                    string destPath = Path.Combine(targetDir, Path.GetFileName(rp.FilePath));
                    
                    if (Directory.Exists(rp.FilePath))
                    {
                        CopyDirectory(rp.FilePath, destPath);
                        result.Status = MoveModStatus.Copied;
                        result.TargetPath = destPath;
                    }
                    else if (File.Exists(rp.FilePath))
                    {
                        File.Copy(rp.FilePath, destPath, true);
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
                DownloadProgress = (i + 1) / (double)_selectedResourcePacksForMove.Count * 100;
            }

            MoveResults = moveResults;
            IsMoveResultDialogVisible = true;
            
            // Reload list
            await LoadResourcePacksListOnlyAsync();
            StatusMessage = $"资源包转移完成";
        }
        catch (Exception ex)
        {
             StatusMessage = $"资源包转移失败: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            IsMoveResourcesDialogVisible = false;
        }
    }

    [RelayCommand]
    private async Task DeleteResourcePack(ResourcePackInfo resourcePack)
    {
        if (resourcePack == null) return;

        var dialog = new ContentDialog
        {
            Title = "删除资源包",
            Content = $"确定要删除资源包 \"{resourcePack.Name}\" 吗？此操作无法撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            if (Directory.Exists(resourcePack.FilePath))
            {
                Directory.Delete(resourcePack.FilePath, true);
            }
            else if (File.Exists(resourcePack.FilePath))
            {
                File.Delete(resourcePack.FilePath);
            }
            
            // 从列表中移除
            ResourcePacks.Remove(resourcePack);
            
            StatusMessage = $"已删除资源包: {resourcePack.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除资源包失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UpdateResourcePacksAsync()
    {
         try
            {
                var selectedPacks = ResourcePacks.Where(r => r.IsSelected).ToList();
                if (selectedPacks.Count == 0)
                {
                    StatusMessage = "请先选择要更新的资源包";
                    return;
                }
                
                IsDownloading = true;
                DownloadProgressDialogTitle = "正在更新资源包...";
                DownloadProgress = 0;
                CurrentDownloadItem = string.Empty;
                
                var packHashes = new List<string>();
                var packFilePathMap = new Dictionary<string, string>();
                
                foreach (var pack in selectedPacks)
                {
                    if (Directory.Exists(pack.FilePath))
                    {
                        continue;
                    }

                    try
                    {
                        string sha1Hash = CalculateSHA1(pack.FilePath);
                        packHashes.Add(sha1Hash);
                        packFilePathMap[sha1Hash] = pack.FilePath;
                    }
                    catch (Exception ex)
                    {
                    }
                }
                
                if (packHashes.Count == 0)
                {
                     StatusMessage = "没有可更新的资源包文件（仅支持.zip文件更新）";
                     IsDownloading = false;
                     return;
                }

                // 获取游戏版本
                string gameVersion = SelectedVersion?.VersionNumber ?? "1.19.2"; 
                 var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                if (versionInfoService != null && SelectedVersion != null)
                {
                    string versionDir = Path.Combine(SelectedVersion.Path);
                    Core.Models.VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(SelectedVersion.Name, versionDir);
                    if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                    {
                        gameVersion = versionConfig.MinecraftVersion;
                    }
                }
                
                string packsPath = GetVersionSpecificPath("resourcepacks");
                
                int updatedCount = 0;
                int upToDateCount = 0;
                
                // Modrinth
                var modrinthResult = await TryUpdateResourcePacksViaModrinthAsync(
                    packHashes, 
                    packFilePathMap, 
                    gameVersion, 
                    packsPath);
                
                updatedCount += modrinthResult.UpdatedCount;
                upToDateCount += modrinthResult.UpToDateCount;
                
                // CurseForge
                var failedPacks = selectedPacks
                    .Where(p => !Directory.Exists(p.FilePath) && !modrinthResult.ProcessedMods.Contains(p.FilePath))
                    .ToList();
                
                if (failedPacks.Count > 0)
                {
                     var curseForgeResult = await TryUpdateResourcePacksViaCurseForgeAsync(
                         failedPacks,
                         gameVersion,
                         packsPath);
                     
                     updatedCount += curseForgeResult.UpdatedCount;
                     upToDateCount += curseForgeResult.UpToDateCount;
                }
                
                await LoadResourcePacksListOnlyAsync();
                
                StatusMessage = $"{updatedCount} 个资源包已更新，{upToDateCount} 个资源包已是最新";
                UpdateResults = StatusMessage;
                IsResultDialogVisible = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"更新资源包失败: {ex.Message}";
                IsResultDialogVisible = true;
                UpdateResults = $"更新失败: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
    }

    private async Task<ModUpdateResult> TryUpdateResourcePacksViaModrinthAsync(
        List<string> hashes,
        Dictionary<string, string> filePathMap,
        string gameVersion,
        string savePath)
    {
        var result = new ModUpdateResult();
        try
        {
            var requestBody = new
            {
                hashes = hashes,
                algorithm = "sha1",
                loaders = new[] { "minecraft" }, // Resource packs just need minecraft loader usually
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
                                            if (File.Exists(filePath)) File.Delete(filePath);
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
            System.Diagnostics.Debug.WriteLine($"[Modrinth] RP更新检查失败: {ex.Message}");
        }
        return result;
    }

    private async Task<ModUpdateResult> TryUpdateResourcePacksViaCurseForgeAsync(
        List<ResourcePackInfo> packs,
        string gameVersion,
        string savePath)
    {
        var result = new ModUpdateResult();
        try
        {
            var fingerprintMap = new Dictionary<uint, string>();
            var fingerprints = new List<uint>();
            
            foreach (var pack in packs)
            {
                 if(Directory.Exists(pack.FilePath)) continue;
                 try 
                 {
                    uint fp = Core.Helpers.CurseForgeFingerprintHelper.ComputeFingerprint(pack.FilePath);
                    fingerprints.Add(fp);
                    fingerprintMap[fp] = pack.FilePath;
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
                            
                        // 资源包通常不需要筛选 ModLoader
                        var latestFile = compatibleFiles.OrderByDescending(f => f.FileDate).FirstOrDefault();
                        if (latestFile != null)
                        {
                             if (latestFile.FileFingerprint != matchedFp)
                             {
                                 string tempFilePath = Path.Combine(savePath, $"{latestFile.FileName}.tmp");
                                 string finalFilePath = Path.Combine(savePath, latestFile.FileName);
                                 
                                 bool downloadSuccess = await DownloadModAsync(latestFile.DownloadUrl, tempFilePath);
                                 if (downloadSuccess)
                                 {
                                     if (File.Exists(filePath)) File.Delete(filePath);
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
             System.Diagnostics.Debug.WriteLine($"[CurseForge] RP更新检查失败: {ex.Message}");
        }
        return result;
    }

    #endregion
}
