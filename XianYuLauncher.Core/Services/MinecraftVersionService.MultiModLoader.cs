using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Minecraft版本服务 - 多加载器组合安装功能部分
/// </summary>
public partial class MinecraftVersionService
{
    /// <summary>
    /// 下载并安装多个 ModLoader 的组合版本
    /// </summary>
    public async Task DownloadMultiModLoaderVersionAsync(
        string minecraftVersionId,
        IEnumerable<ModLoaderSelection> modLoaderSelections,
        string minecraftDirectory,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default,
        string? customVersionName = null)
    {
        var selections = modLoaderSelections.OrderBy(s => s.InstallOrder).ToList();
        
        if (selections.Count == 0)
        {
            throw new ArgumentException("至少需要选择一个 ModLoader", nameof(modLoaderSelections));
        }

        try
        {
            _logger.LogInformation("开始多加载器组合安装: {Loaders} for Minecraft {Version}",
                string.Join(" + ", selections.Select(s => $"{s.Type} {s.Version}")),
                minecraftVersionId);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 0));

            // 生成版本 ID
            var versionId = GenerateMultiLoaderVersionId(minecraftVersionId, selections, customVersionName);
            _logger.LogInformation("生成版本 ID: {VersionId}", versionId);

            // 分配进度权重
            var progressWeights = CalculateProgressWeights(selections);
            double currentProgress = 0;

            // 依次安装每个加载器
            for (int i = 0; i < selections.Count; i++)
            {
                var selection = selections[i];
                var weight = progressWeights[i];
                var startProgress = currentProgress;
                var endProgress = currentProgress + weight;

                _logger.LogInformation("===== 开始安装 {Type} {Version} ({Order}/{Total}) =====",
                    selection.Type, selection.Version, i + 1, selections.Count);

                if (selection.IsAddon)
                {
                    // 作为附加组件安装（如 OptiFine 作为 Mod）
                    await InstallAsAddonAsync(
                        selection,
                        minecraftVersionId,
                        versionId,
                        minecraftDirectory,
                        status => ReportProgress(progressCallback, status, startProgress, endProgress),
                        cancellationToken);
                }
                else
                {
                    // 作为基础加载器安装
                    await InstallAsBaseLoaderAsync(
                        selection,
                        minecraftVersionId,
                        versionId,
                        minecraftDirectory,
                        i == 0, // 第一个加载器使用自定义版本名
                        status => ReportProgress(progressCallback, status, startProgress, endProgress),
                        cancellationToken);
                }

                currentProgress = endProgress;
                _logger.LogInformation("===== {Type} 安装完成 =====", selection.Type);
            }

            progressCallback?.Invoke(new DownloadProgressStatus(100, 100, 100));
            _logger.LogInformation("多加载器组合安装完成: {VersionId}", versionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "多加载器组合安装失败: {Loaders} for Minecraft {Version}",
                string.Join(" + ", selections.Select(s => $"{s.Type} {s.Version}")),
                minecraftVersionId);
            throw;
        }
    }

    /// <summary>
    /// 作为基础加载器安装
    /// </summary>
    private async Task InstallAsBaseLoaderAsync(
        ModLoaderSelection selection,
        string minecraftVersionId,
        string targetVersionId,
        string minecraftDirectory,
        bool useCustomVersionName,
        Action<DownloadProgressStatus> progressCallback,
        CancellationToken cancellationToken)
    {
        var installer = _modLoaderInstallerFactory.GetInstaller(selection.Type);
        
        await installer.InstallAsync(
            minecraftVersionId,
            selection.Version,
            minecraftDirectory,
            progressCallback,
            cancellationToken,
            useCustomVersionName ? targetVersionId : null);
    }

    /// <summary>
    /// 作为附加组件安装（如 OptiFine 作为 Mod 安装到 Forge，或 LiteLoader 作为 Tweaker 安装）
    /// </summary>
    private async Task InstallAsAddonAsync(
        ModLoaderSelection selection,
        string minecraftVersionId,
        string targetVersionId,
        string minecraftDirectory,
        Action<DownloadProgressStatus> progressCallback,
        CancellationToken cancellationToken)
    {
        if (selection.Type.Equals("OptiFine", StringComparison.OrdinalIgnoreCase))
        {
            await InstallOptifineAsModAsync(
                minecraftVersionId,
                selection.Version,
                targetVersionId,
                minecraftDirectory,
                progressCallback,
                cancellationToken);
        }
        else if (selection.Type.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase))
        {
            // LiteLoader 作为 Addon 安装时，使用安装器的 Addon 模式
            var installer = _modLoaderInstallerFactory.GetInstaller(selection.Type);
            var options = new ModLoaderInstallOptions
            {
                CustomVersionName = targetVersionId, // 指定现有版本，触发 Addon 模式
                OverwriteExisting = false
            };
            
            await installer.InstallAsync(
                minecraftVersionId,
                selection.Version,
                minecraftDirectory,
                options,
                progressCallback,
                cancellationToken);
        }
        else
        {
            throw new NotSupportedException($"不支持将 {selection.Type} 作为附加组件安装");
        }
    }

    /// <summary>
    /// 将 OptiFine 作为 Mod 安装到指定版本
    /// </summary>
    private async Task InstallOptifineAsModAsync(
        string minecraftVersionId,
        string optifineVersion,
        string targetVersionId,
        string minecraftDirectory,
        Action<DownloadProgressStatus> progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("将 OptiFine 作为 Mod 安装到版本: {VersionId}", targetVersionId);

        var versionsDirectory = Path.Combine(minecraftDirectory, "versions");
        var versionDirectory = Path.Combine(versionsDirectory, targetVersionId);
        var modsDirectory = Path.Combine(versionDirectory, "mods");
        Directory.CreateDirectory(modsDirectory);

        // 解析 OptiFine 版本格式（如 "HD_U:I5"）
        var parts = optifineVersion.Split(':');
        if (parts.Length != 2)
        {
            throw new ArgumentException($"OptiFine 版本格式错误: {optifineVersion}，应为 'Type:Patch' 格式");
        }

        var optifineType = parts[0];
        var optifinePatch = parts[1];

        var optifineJarName = $"OptiFine_{minecraftVersionId}_{optifineType}_{optifinePatch}.jar";
        var optifineJarPath = Path.Combine(modsDirectory, optifineJarName);
        var optifineDownloadUrl = $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersionId}/{optifineType}/{optifinePatch}";

        _logger.LogInformation("下载 OptiFine JAR: {Url}", optifineDownloadUrl);

        var downloadResult = await _downloadManager.DownloadFileAsync(
            optifineDownloadUrl,
            optifineJarPath,
            null,
            progressCallback,
            cancellationToken);

        if (!downloadResult.Success)
        {
            throw new Exception($"下载 OptiFine 失败: {downloadResult.ErrorMessage}");
        }

        _logger.LogInformation("OptiFine JAR 下载完成: {Path}", optifineJarPath);
    }

    /// <summary>
    /// 生成多加载器组合的版本 ID
    /// </summary>
    private string GenerateMultiLoaderVersionId(
        string minecraftVersionId,
        List<ModLoaderSelection> selections,
        string? customVersionName)
    {
        if (!string.IsNullOrEmpty(customVersionName))
        {
            return customVersionName;
        }

        // 格式：forge-1.20.1-49.0.3-optifine-HD_U_I5
        var parts = new List<string>();
        
        foreach (var selection in selections.OrderBy(s => s.InstallOrder))
        {
            var loaderType = selection.Type.ToLower();
            var version = selection.Version.Replace(":", "_");
            
            if (parts.Count == 0)
            {
                // 第一个加载器包含 Minecraft 版本
                parts.Add($"{loaderType}-{minecraftVersionId}-{version}");
            }
            else
            {
                // 后续加载器只添加类型和版本
                parts.Add($"{loaderType}-{version}");
            }
        }

        return string.Join("-", parts);
    }

    /// <summary>
    /// 计算每个加载器的进度权重
    /// </summary>
    private List<double> CalculateProgressWeights(List<ModLoaderSelection> selections)
    {
        // 基础加载器权重更高（80%），附加组件权重较低（20%）
        var weights = new List<double>();
        var baseLoaderCount = selections.Count(s => !s.IsAddon);
        var addonCount = selections.Count(s => s.IsAddon);

        var baseLoaderWeight = baseLoaderCount > 0 ? 80.0 / baseLoaderCount : 0;
        var addonWeight = addonCount > 0 ? 20.0 / addonCount : 0;

        foreach (var selection in selections)
        {
            weights.Add(selection.IsAddon ? addonWeight : baseLoaderWeight);
        }

        return weights;
    }

    /// <summary>
    /// 报告进度（映射到指定范围）
    /// </summary>
    private void ReportProgress(
        Action<DownloadProgressStatus>? callback,
        DownloadProgressStatus status,
        double startPercent,
        double endPercent)
    {
        if (callback == null) return;

        var mappedPercent = startPercent + (status.Percent / 100.0) * (endPercent - startPercent);
        callback(new DownloadProgressStatus(
            status.DownloadedBytes,
            status.TotalBytes,
            mappedPercent,
            status.BytesPerSecond));
    }
}
