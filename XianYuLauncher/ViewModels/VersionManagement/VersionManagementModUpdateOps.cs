using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.ViewModels;

internal static class VersionManagementModUpdateOps
{
    public static async Task<ModUpdateResult> TryUpdateModsViaModrinthAsync(
        ModrinthService modrinthService,
        List<string> modHashes,
        Dictionary<string, string> modFilePathMap,
        string modLoader,
        string gameVersion,
        string modsPath,
        Func<string, string> calculateSha1,
        Func<string, string, Task<bool>> downloadModAsync,
        Func<List<Dependency>, string, Task<int>> processDependenciesAsync)
    {
        var result = new ModUpdateResult();

        try
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth] 请求更新信息，Mod数量: {modHashes.Count}");

            var updateInfo = await modrinthService.UpdateVersionFilesAsync(
                modHashes,
                new[] { modLoader },
                new[] { gameVersion });

            if (updateInfo != null && updateInfo.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 找到 {updateInfo.Count} 个Mod的更新信息");

                foreach (var kvp in updateInfo)
                {
                    var hash = kvp.Key;
                    var info = kvp.Value;

                    if (!modFilePathMap.TryGetValue(hash, out var modFilePath))
                    {
                        continue;
                    }

                    result.ProcessedMods.Add(modFilePath);
                    var needsUpdate = true;

                    if (info.Files != null && info.Files.Count > 0)
                    {
                        var primaryFile = info.Files.FirstOrDefault(file => file.Primary) ?? info.Files[0];
                        if (primaryFile.Hashes.TryGetValue("sha1", out var newSha1))
                        {
                            var currentSha1 = calculateSha1(modFilePath);
                            if (currentSha1.Equals(newSha1, StringComparison.OrdinalIgnoreCase))
                            {
                                System.Diagnostics.Debug.WriteLine($"[Modrinth] Mod {Path.GetFileName(modFilePath)} 已经是最新版本");
                                needsUpdate = false;
                                result.UpToDateCount++;
                            }
                        }
                    }

                    if (!needsUpdate)
                    {
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 正在更新Mod: {Path.GetFileName(modFilePath)}");
                    var latestFile = info.Files.FirstOrDefault(file => file.Primary) ?? info.Files[0];
                    if (string.IsNullOrEmpty(latestFile.Url?.ToString()) || string.IsNullOrEmpty(latestFile.Filename))
                    {
                        continue;
                    }

                    var tempFilePath = Path.Combine(modsPath, $"{latestFile.Filename}.tmp");
                    var finalFilePath = Path.Combine(modsPath, latestFile.Filename);

                    var downloadSuccess = await downloadModAsync(latestFile.Url.ToString(), tempFilePath);
                    if (!downloadSuccess)
                    {
                        continue;
                    }

                    if (info.Dependencies != null && info.Dependencies.Count > 0)
                    {
                        await processDependenciesAsync(info.Dependencies, modsPath);
                    }

                    if (File.Exists(modFilePath))
                    {
                        File.Delete(modFilePath);
                        System.Diagnostics.Debug.WriteLine($"[Modrinth] 已删除旧Mod文件: {modFilePath}");
                    }

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
            else
            {
                System.Diagnostics.Debug.WriteLine("[Modrinth] 没有找到任何Mod的更新信息");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth] 更新失败: {ex.Message}");
        }

        return result;
    }

    public static async Task<ModUpdateResult> TryUpdateModsViaCurseForgeAsync(
        CurseForgeService curseForgeService,
        List<ModInfo> mods,
        string modLoader,
        string gameVersion,
        string modsPath,
        Action<string, double> onDownloadProgress)
    {
        var result = new ModUpdateResult();

        try
        {
            var fingerprintMap = new Dictionary<uint, string>();
            var fingerprints = new List<uint>();

            foreach (var mod in mods)
            {
                try
                {
                    var fingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(mod.FilePath);
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
                System.Diagnostics.Debug.WriteLine("[CurseForge] 没有可查询的Fingerprint");
                return result;
            }

            System.Diagnostics.Debug.WriteLine($"[CurseForge] 查询 {fingerprints.Count} 个Mod的Fingerprint");
            var matchResult = await curseForgeService.GetFingerprintMatchesAsync(fingerprints);

            if (matchResult.ExactMatches != null && matchResult.ExactMatches.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge] 找到 {matchResult.ExactMatches.Count} 个精确匹配");
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
                    {
                        continue;
                    }

                    uint matchedFingerprint = (uint)match.File.FileFingerprint;
                    if (!fingerprintMap.TryGetValue(matchedFingerprint, out var modFilePath))
                    {
                        continue;
                    }

                    result.ProcessedMods.Add(modFilePath);
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 处理Mod: {Path.GetFileName(modFilePath)}");

                    CurseForgeFile? latestFile = null;
                    if (match.LatestFiles != null && match.LatestFiles.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] LatestFiles 数量: {match.LatestFiles.Count}");
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 当前游戏版本: {gameVersion}, ModLoader: {modLoader}");

                        for (var index = 0; index < match.LatestFiles.Count; index++)
                        {
                            var file = match.LatestFiles[index];
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 文件 {index + 1}: {file.FileName}");
                            System.Diagnostics.Debug.WriteLine($"[CurseForge]   - 支持的版本: {string.Join(", ", file.GameVersions ?? new List<string>())}");
                            System.Diagnostics.Debug.WriteLine($"[CurseForge]   - 文件日期: {file.FileDate}");
                        }

                        var compatibleFiles = match.LatestFiles
                            .Where(file => file.GameVersions != null && file.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                            .ToList();
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 游戏版本兼容的文件数量: {compatibleFiles.Count}");

                        if (modLoaderType.HasValue)
                        {
                            var loaderCompatibleFiles = compatibleFiles
                                .Where(file => file.GameVersions.Any(version => version.Equals(modLoader, StringComparison.OrdinalIgnoreCase)))
                                .ToList();
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] ModLoader 兼容的文件数量: {loaderCompatibleFiles.Count}");
                            if (loaderCompatibleFiles.Count > 0)
                            {
                                compatibleFiles = loaderCompatibleFiles;
                            }
                        }

                        latestFile = compatibleFiles
                            .OrderByDescending(file => file.FileDate)
                            .FirstOrDefault();

                        if (latestFile != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 选择的文件: {latestFile.FileName}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[CurseForge] LatestFiles 为空或数量为 0");
                    }

                    if (latestFile == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[CurseForge] 没有找到兼容的文件");
                        continue;
                    }

                    var needsUpdate = match.File.Id != latestFile.Id;
                    if (!needsUpdate)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] Mod {Path.GetFileName(modFilePath)} 已经是最新版本");
                        result.UpToDateCount++;
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 正在更新Mod: {Path.GetFileName(modFilePath)}");
                    if (string.IsNullOrEmpty(latestFile.DownloadUrl) || string.IsNullOrEmpty(latestFile.FileName))
                    {
                        continue;
                    }

                    var tempFilePath = Path.Combine(modsPath, $"{latestFile.FileName}.tmp");
                    var finalFilePath = Path.Combine(modsPath, latestFile.FileName);

                    var downloadSuccess = await curseForgeService.DownloadFileAsync(
                        latestFile.DownloadUrl,
                        tempFilePath,
                        onDownloadProgress);

                    if (!downloadSuccess)
                    {
                        continue;
                    }

                    if (latestFile.Dependencies != null && latestFile.Dependencies.Count > 0)
                    {
                        await curseForgeService.ProcessDependenciesAsync(
                            latestFile.Dependencies,
                            modsPath,
                            latestFile);
                    }

                    if (File.Exists(modFilePath))
                    {
                        File.Delete(modFilePath);
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 已删除旧Mod文件: {modFilePath}");
                    }

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
            else
            {
                System.Diagnostics.Debug.WriteLine("[CurseForge] 没有找到任何精确匹配");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForge] 更新失败: {ex.Message}");
        }

        return result;
    }
}
