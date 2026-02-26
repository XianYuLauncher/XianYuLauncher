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
}

/// <summary>
/// 自动测速服务实现
/// </summary>
public class AutoSpeedTestService : IAutoSpeedTestService
{
    private readonly ISpeedTestService _speedTestService;
    private const int CacheExpirationHours = 12;

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
            var gameResults = await _speedTestService.TestGameSourcesAsync();
            var communityResults = await _speedTestService.TestCommunitySourcesAsync();
            var curseforgeResults = await _speedTestService.TestCurseForgeSourcesAsync();

            var cache = new SpeedTestCache
            {
                GameSources = gameResults.ToDictionary(r => r.SourceKey),
                CommunitySources = communityResults.ToDictionary(r => r.SourceKey),
                CurseForgeSources = curseforgeResults.ToDictionary(r => r.SourceKey),
                LastUpdated = DateTime.UtcNow
            };

            await _speedTestService.SaveCacheAsync(cache);

            Log.Information("[AutoSpeedTest] 自动测速完成，游戏源: {GameCount}, 社区源: {CommunityCount}, CurseForge源: {CurseForgeCount}",
                gameResults.Count, communityResults.Count, curseforgeResults.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AutoSpeedTest] 测速执行失败");
        }
    }
}
