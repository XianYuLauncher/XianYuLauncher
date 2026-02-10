using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

public class LiteLoaderService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly FallbackDownloadManager _fallbackDownloadManager;

    private const string SUPPORTED_VERSIONS_LOG = "1.5.2, 1.6.2, 1.6.4, 1.7.2, 1.7.10, 1.8, 1.8.9, 1.9, 1.9.4, 1.10, 1.10.2, 1.11, 1.11.2, 1.12, 1.12.1, 1.12.2";

    private static readonly HashSet<string> _supportedVersions;

    static LiteLoaderService()
    {
        _supportedVersions = new HashSet<string>(
            SUPPORTED_VERSIONS_LOG.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries));
    }

    public LiteLoaderService(
        HttpClient httpClient,
        DownloadSourceFactory downloadSourceFactory,
        FallbackDownloadManager fallbackDownloadManager)
    {
        _httpClient = httpClient;
        _downloadSourceFactory = downloadSourceFactory;
        _fallbackDownloadManager = fallbackDownloadManager;
    }

    /// <summary>
    /// 检查指定的 Minecraft 版本是否支持 LiteLoader
    /// </summary>
    public bool IsLiteLoaderSupported(string minecraftVersion)
    {
        if (string.IsNullOrEmpty(minecraftVersion)) return false;
        return _supportedVersions.Contains(minecraftVersion);
    }

    /// <summary>
    /// 获取 LiteLoader 版本列表
    /// </summary>
    public async Task<LiteLoaderRoot?> GetLiteLoaderVersionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _fallbackDownloadManager.SendGetWithFallbackAsync(
                    source => source.GetLiteLoaderVersionsUrl(),
                    (request, source) =>
                    {
                        if (source.Name == "BMCLAPI")
                        {
                            request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
                        }
                    });

            if (result.Success && result.Response != null)
            {
                result.Response.EnsureSuccessStatusCode();
                string json = await result.Response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<LiteLoaderRoot>(json);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiteLoaderService] 获取版本列表失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取特定 Minecraft 版本的 LiteLoader 构件列表（包含 Snapshot 和 Release）
    /// </summary>
    public async Task<List<LiteLoaderArtifact>> GetLiteLoaderArtifactsAsync(string minecraftVersion, CancellationToken token = default)
    {
        var results = new List<LiteLoaderArtifact>();
        if (!IsLiteLoaderSupported(minecraftVersion)) return results;

        var root = await GetLiteLoaderVersionsAsync(token);
        if (root?.Versions != null && root.Versions.TryGetValue(minecraftVersion, out var mcVersion))
        {
            // 1. 处理 Snapshot 版本 (快照)
            if (mcVersion.Snapshots?.Artifacts != null && 
                mcVersion.Snapshots.Artifacts.TryGetValue("latest", out var snapshotArtifact))
            {
                snapshotArtifact.BaseUrl = mcVersion.Repo?.Url;
                // 注意：不合并 Snapshots.Libraries，这些是开发用的库，启动器不需要
                // 只使用 artifact 自己的 Libraries
                results.Add(snapshotArtifact);
            }

            // 2. 处理 Artefacts 版本 (正式版/稳定版)
            if (mcVersion.Artefacts?.Artifacts != null &&
                mcVersion.Artefacts.Artifacts.TryGetValue("latest", out var releaseArtifact))
            {
                releaseArtifact.BaseUrl = mcVersion.Repo?.Url;
                // 同样不合并外部 libraries
                results.Add(releaseArtifact);
            }
        }

        return results;
    }
}
