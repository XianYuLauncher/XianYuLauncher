using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.ViewModels;

internal static class VersionManagementResourceUpdateOps
{
    public static async Task<ModUpdateResult> TryUpdateResourcePacksViaModrinthAsync(
        ModrinthService modrinthService,
        List<string> hashes,
        Dictionary<string, string> filePathMap,
        string gameVersion,
        string savePath,
        Func<string, string, Task<bool>> downloadModAsync,
        Func<string, string> calculateSha1)
    {
        var result = new ModUpdateResult();
        try
        {
            var updateInfo = await modrinthService.UpdateVersionFilesAsync(
                hashes,
                new[] { "minecraft" },
                new[] { gameVersion });

            if (updateInfo != null && updateInfo.Count > 0)
            {
                foreach (var kvp in updateInfo)
                {
                    var hash = kvp.Key;
                    var info = kvp.Value;

                    if (!filePathMap.TryGetValue(hash, out var filePath))
                    {
                        continue;
                    }

                    result.ProcessedMods.Add(filePath);
                    var needsUpdate = true;
                    if (info.Files != null && info.Files.Count > 0)
                    {
                        var primaryFile = info.Files.FirstOrDefault(f => f.Primary) ?? info.Files[0];
                        if (primaryFile.Hashes.TryGetValue("sha1", out var newSha1))
                        {
                            var currentSha1 = calculateSha1(filePath);
                            if (currentSha1.Equals(newSha1, StringComparison.OrdinalIgnoreCase))
                            {
                                needsUpdate = false;
                                result.UpToDateCount++;
                            }
                        }
                    }

                    if (!needsUpdate)
                    {
                        continue;
                    }

                    var latestFile = info.Files.FirstOrDefault(f => f.Primary) ?? info.Files[0];
                    if (string.IsNullOrEmpty(latestFile.Url?.ToString()) || string.IsNullOrEmpty(latestFile.Filename))
                    {
                        continue;
                    }

                    var tempFilePath = Path.Combine(savePath, $"{latestFile.Filename}.tmp");
                    var finalFilePath = Path.Combine(savePath, latestFile.Filename);

                    var downloadSuccess = await downloadModAsync(latestFile.Url.ToString(), tempFilePath);
                    if (!downloadSuccess)
                    {
                        continue;
                    }

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    if (File.Exists(finalFilePath))
                    {
                        File.Delete(finalFilePath);
                    }

                    File.Move(tempFilePath, finalFilePath);
                    result.UpdatedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth] RP更新检查失败: {ex.Message}");
        }

        return result;
    }

    public static async Task<ModUpdateResult> TryUpdateResourcePacksViaCurseForgeAsync(
        CurseForgeService curseForgeService,
        List<ResourcePackInfo> packs,
        string gameVersion,
        string savePath,
        Func<string, string, Task<bool>> downloadModAsync)
    {
        var result = new ModUpdateResult();
        try
        {
            var fingerprintMap = new Dictionary<uint, string>();
            var fingerprints = new List<uint>();

            foreach (var pack in packs)
            {
                if (Directory.Exists(pack.FilePath))
                {
                    continue;
                }

                try
                {
                    var fingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(pack.FilePath);
                    fingerprints.Add(fingerprint);
                    fingerprintMap[fingerprint] = pack.FilePath;
                }
                catch
                {
                }
            }

            if (fingerprints.Count == 0)
            {
                return result;
            }

            var matchResult = await curseForgeService.GetFingerprintMatchesAsync(fingerprints);
            if (matchResult.ExactMatches != null)
            {
                foreach (var match in matchResult.ExactMatches)
                {
                    if (match.File == null)
                    {
                        continue;
                    }

                    var matchedFingerprint = (uint)match.File.FileFingerprint;
                    if (!fingerprintMap.TryGetValue(matchedFingerprint, out var filePath))
                    {
                        continue;
                    }

                    result.ProcessedMods.Add(filePath);

                    if (match.LatestFiles == null || match.LatestFiles.Count == 0)
                    {
                        continue;
                    }

                    var compatibleFiles = match.LatestFiles
                        .Where(file => file.GameVersions != null && file.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    var latestFile = compatibleFiles.OrderByDescending(file => file.FileDate).FirstOrDefault();
                    if (latestFile == null)
                    {
                        continue;
                    }

                    if (latestFile.FileFingerprint == matchedFingerprint)
                    {
                        result.UpToDateCount++;
                        continue;
                    }

                    var tempFilePath = Path.Combine(savePath, $"{latestFile.FileName}.tmp");
                    var finalFilePath = Path.Combine(savePath, latestFile.FileName);

                    var downloadSuccess = await downloadModAsync(latestFile.DownloadUrl, tempFilePath);
                    if (!downloadSuccess)
                    {
                        continue;
                    }

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    if (File.Exists(finalFilePath))
                    {
                        File.Delete(finalFilePath);
                    }

                    File.Move(tempFilePath, finalFilePath);
                    result.UpdatedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForge] RP更新检查失败: {ex.Message}");
        }

        return result;
    }

    public static async Task<ModUpdateResult> TryUpdateShadersViaModrinthAsync(
        ModrinthService modrinthService,
        List<string> hashes,
        Dictionary<string, string> filePathMap,
        string gameVersion,
        string savePath,
        Func<string, string, Task<bool>> downloadModAsync,
        Func<string, string> calculateSha1)
    {
        var result = new ModUpdateResult();
        try
        {
            var updateInfo = await modrinthService.UpdateVersionFilesAsync(
                hashes,
                new[] { "iris", "optifine", "minecraft" },
                new[] { gameVersion });

            if (updateInfo != null && updateInfo.Count > 0)
            {
                foreach (var kvp in updateInfo)
                {
                    var hash = kvp.Key;
                    var info = kvp.Value;

                    if (!filePathMap.TryGetValue(hash, out var filePath))
                    {
                        continue;
                    }

                    result.ProcessedMods.Add(filePath);
                    var needsUpdate = true;
                    if (info.Files != null && info.Files.Count > 0)
                    {
                        var primaryFile = info.Files.FirstOrDefault(f => f.Primary) ?? info.Files[0];
                        if (primaryFile.Hashes.TryGetValue("sha1", out var newSha1))
                        {
                            var currentSha1 = calculateSha1(filePath);
                            if (currentSha1.Equals(newSha1, StringComparison.OrdinalIgnoreCase))
                            {
                                needsUpdate = false;
                                result.UpToDateCount++;
                            }
                        }
                    }

                    if (!needsUpdate)
                    {
                        continue;
                    }

                    var latestFile = info.Files.FirstOrDefault(f => f.Primary) ?? info.Files[0];
                    if (string.IsNullOrEmpty(latestFile.Url?.ToString()) || string.IsNullOrEmpty(latestFile.Filename))
                    {
                        continue;
                    }

                    var tempFilePath = Path.Combine(savePath, $"{latestFile.Filename}.tmp");
                    var finalFilePath = Path.Combine(savePath, latestFile.Filename);

                    var downloadSuccess = await downloadModAsync(latestFile.Url.ToString(), tempFilePath);
                    if (!downloadSuccess)
                    {
                        continue;
                    }

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        var configFilePath = $"{filePath}.txt";
                        if (File.Exists(configFilePath))
                        {
                            File.Delete(configFilePath);
                        }
                    }

                    if (File.Exists(finalFilePath))
                    {
                        File.Delete(finalFilePath);
                    }

                    File.Move(tempFilePath, finalFilePath);
                    result.UpdatedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth] 光影更新检查失败: {ex.Message}");
        }

        return result;
    }

    public static async Task<ModUpdateResult> TryUpdateShadersViaCurseForgeAsync(
        CurseForgeService curseForgeService,
        List<ShaderInfo> shaders,
        string gameVersion,
        string savePath,
        Func<string, string, Task<bool>> downloadModAsync)
    {
        var result = new ModUpdateResult();
        try
        {
            var fingerprintMap = new Dictionary<uint, string>();
            var fingerprints = new List<uint>();

            foreach (var shader in shaders)
            {
                if (Directory.Exists(shader.FilePath))
                {
                    continue;
                }

                try
                {
                    var fingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(shader.FilePath);
                    fingerprints.Add(fingerprint);
                    fingerprintMap[fingerprint] = shader.FilePath;
                }
                catch
                {
                }
            }

            if (fingerprints.Count == 0)
            {
                return result;
            }

            var matchResult = await curseForgeService.GetFingerprintMatchesAsync(fingerprints);
            if (matchResult.ExactMatches != null)
            {
                foreach (var match in matchResult.ExactMatches)
                {
                    if (match.File == null)
                    {
                        continue;
                    }

                    var matchedFingerprint = (uint)match.File.FileFingerprint;
                    if (!fingerprintMap.TryGetValue(matchedFingerprint, out var filePath))
                    {
                        continue;
                    }

                    result.ProcessedMods.Add(filePath);

                    if (match.LatestFiles == null || match.LatestFiles.Count == 0)
                    {
                        continue;
                    }

                    var compatibleFiles = match.LatestFiles
                        .Where(file => file.GameVersions != null && file.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    var latestFile = compatibleFiles.OrderByDescending(file => file.FileDate).FirstOrDefault();
                    if (latestFile == null)
                    {
                        continue;
                    }

                    if (latestFile.FileFingerprint == matchedFingerprint)
                    {
                        result.UpToDateCount++;
                        continue;
                    }

                    var tempFilePath = Path.Combine(savePath, $"{latestFile.FileName}.tmp");
                    var finalFilePath = Path.Combine(savePath, latestFile.FileName);

                    var downloadSuccess = await downloadModAsync(latestFile.DownloadUrl, tempFilePath);
                    if (!downloadSuccess)
                    {
                        continue;
                    }

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        var configFilePath = $"{filePath}.txt";
                        if (File.Exists(configFilePath))
                        {
                            File.Delete(configFilePath);
                        }
                    }

                    if (File.Exists(finalFilePath))
                    {
                        File.Delete(finalFilePath);
                    }

                    File.Move(tempFilePath, finalFilePath);
                    result.UpdatedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForge] 光影更新检查失败: {ex.Message}");
        }

        return result;
    }
}
