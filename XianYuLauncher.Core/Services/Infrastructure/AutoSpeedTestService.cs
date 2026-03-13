using XianYuLauncher.Core.Models;
using Serilog;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 自动测速服务接口
/// </summary>
public interface IAutoSpeedTestService
{
    /// <summary>
    /// 检查并执行自动测速（缓存为空或过期时）
    /// </summary>
    Task CheckAndRunAsync();

    /// <summary>
    /// 自动测速完成事件（仅在实际执行测速并写入缓存后触发）
    /// </summary>
    event EventHandler? SpeedTestCompleted;
}

/// <summary>
/// 自动测速服务实现
/// </summary>
public class AutoSpeedTestService : IAutoSpeedTestService
{
    private readonly ISpeedTestService _speedTestService;
    private const int CacheExpirationHours = 12;

    /// <inheritdoc />
    public event EventHandler? SpeedTestCompleted;

    public AutoSpeedTestService(ISpeedTestService speedTestService)
    {
        _speedTestService = speedTestService;
    }

    /// <inheritdoc />
    public async Task CheckAndRunAsync()
    {
        try
        {
            var cache = await _speedTestService.LoadCacheAsync();

            // 无缓存（或缓存未初始化）时测速
            if (cache.LastUpdated == default)
            {
                Log.Information("[AutoSpeedTest] 未检测到测速缓存，执行自动测速");
                await RunSpeedTestAsync();
                return;
            }

            // 缓存已过期时测速
            if (cache.IsExpired)
            {
                Log.Information("[AutoSpeedTest] 测速缓存已过期（{Hours}小时），执行自动测速", CacheExpirationHours);
                await RunSpeedTestAsync();
                return;
            }

            Log.Information("[AutoSpeedTest] 测速缓存仍然有效，跳过自动测速");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AutoSpeedTest] 自动测速检查失败");
        }
    }

    /// <summary>
    /// 执行测速并保存结果
    /// </summary>
    private async Task RunSpeedTestAsync()
    {
        try
        {
            var versionManifestResults = await _speedTestService.TestVersionManifestSourcesAsync();
            var fileDownloadResults = await _speedTestService.TestFileDownloadSourcesAsync();
            var communityResults = await _speedTestService.TestCommunitySourcesAsync();
            var curseforgeResults = await _speedTestService.TestCurseForgeSourcesAsync();
            var forgeResults = await _speedTestService.TestForgeSourcesAsync();
            var fabricResults = await _speedTestService.TestFabricSourcesAsync();
            var neoforgeResults = await _speedTestService.TestNeoForgeSourcesAsync();
            var liteLoaderResults = await _speedTestService.TestModLoaderSourcesAsync("liteloader");
            var quiltResults = await _speedTestService.TestModLoaderSourcesAsync("quilt");
            var legacyFabricResults = await _speedTestService.TestModLoaderSourcesAsync("legacyfabric");
            var cleanroomResults = await _speedTestService.TestModLoaderSourcesAsync("cleanroom");
            var optifineResults = await _speedTestService.TestModLoaderSourcesAsync("optifine");

            var cache = new SpeedTestCache
            {
                VersionManifestSources = versionManifestResults.ToDictionary(r => r.SourceKey),
                FileDownloadSources = fileDownloadResults.ToDictionary(r => r.SourceKey),
                CommunitySources = communityResults.ToDictionary(r => r.SourceKey),
                CurseForgeSources = curseforgeResults.ToDictionary(r => r.SourceKey),
                ForgeSources = forgeResults.ToDictionary(r => r.SourceKey),
                FabricSources = fabricResults.ToDictionary(r => r.SourceKey),
                NeoForgeSources = neoforgeResults.ToDictionary(r => r.SourceKey),
                LiteLoaderSources = liteLoaderResults.ToDictionary(r => r.SourceKey),
                QuiltSources = quiltResults.ToDictionary(r => r.SourceKey),
                LegacyFabricSources = legacyFabricResults.ToDictionary(r => r.SourceKey),
                CleanroomSources = cleanroomResults.ToDictionary(r => r.SourceKey),
                OptifineSources = optifineResults.ToDictionary(r => r.SourceKey),
                LastUpdated = DateTime.UtcNow
            };

            await _speedTestService.SaveCacheAsync(cache);

            Log.Information("[AutoSpeedTest] 自动测速完成，版本清单源: {VersionManifestCount}, 文件下载源: {FileDownloadCount}, 社区源: {CommunityCount}, CurseForge源: {CurseForgeCount}, Forge源: {ForgeCount}, Fabric源: {FabricCount}, NeoForge源: {NeoForgeCount}, LiteLoader源: {LiteLoaderCount}, Quilt源: {QuiltCount}, LegacyFabric源: {LegacyFabricCount}, Cleanroom源: {CleanroomCount}, OptiFine源: {OptifineCount}",
                versionManifestResults.Count,
                fileDownloadResults.Count,
                communityResults.Count,
                curseforgeResults.Count,
                forgeResults.Count,
                fabricResults.Count,
                neoforgeResults.Count,
                liteLoaderResults.Count,
                quiltResults.Count,
                legacyFabricResults.Count,
                cleanroomResults.Count,
                optifineResults.Count);

            SpeedTestCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AutoSpeedTest] 测速执行失败");
        }
    }
}
