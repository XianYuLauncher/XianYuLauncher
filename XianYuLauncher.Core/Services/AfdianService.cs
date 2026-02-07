using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 爱发电API服务实现
/// </summary>
public class AfdianService : IAfdianService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFileService _fileService;
    private const string CacheFileName = "afdian_sponsors_cache.json";
    private const string ApiEndpoint = "https://afdian.com/api/open/query-sponsor";
    
    public AfdianService(IHttpClientFactory httpClientFactory, IFileService fileService)
    {
        _httpClientFactory = httpClientFactory;
        _fileService = fileService;
    }
    
    /// <summary>
    /// 获取赞助者列表（带缓存）
    /// </summary>
    public async Task<List<AfdianSponsor>> GetSponsorsAsync()
    {
        // 从 SecretsService 读取凭据
        var config = SecretsService.Config.Afdian;
        
        // 检查凭据
        if (string.IsNullOrEmpty(config.UserId) || string.IsNullOrEmpty(config.Token))
        {
            Log.Warning("[AfdianService] 爱发电凭据未配置");
            return new List<AfdianSponsor>();
        }
        
        // 尝试从缓存加载
        var cache = await LoadCacheAsync();
        if (cache != null && !cache.IsExpired)
        {
            Log.Information($"[AfdianService] 从缓存加载赞助者列表，共 {cache.Sponsors.Count} 人");
            return cache.Sponsors;
        }
        
        // 缓存过期或不存在，从API获取
        return await RefreshSponsorsAsync();
    }
    
    /// <summary>
    /// 强制刷新赞助者列表
    /// </summary>
    public async Task<List<AfdianSponsor>> RefreshSponsorsAsync()
    {
        try
        {
            var config = SecretsService.Config.Afdian;
            
            if (string.IsNullOrEmpty(config.UserId) || string.IsNullOrEmpty(config.Token))
            {
                Log.Warning("[AfdianService] 爱发电凭据未配置");
                return new List<AfdianSponsor>();
            }
            
            var allSponsors = new List<AfdianSponsor>();
            int page = 1;
            
            // 分页获取所有赞助者
            do
            {
                var sponsors = await FetchSponsorsPageAsync(page, config.UserId, config.Token);
                if (sponsors == null || sponsors.Count == 0)
                {
                    break;
                }
                
                allSponsors.AddRange(sponsors);
                page++;
                
                // 防止无限循环，最多获取10页
                if (page > 10)
                {
                    break;
                }
                
            } while (true);
            
            // 保存到缓存
            await SaveCacheAsync(new AfdianSponsorCache
            {
                Sponsors = allSponsors,
                CachedAt = DateTime.UtcNow
            });
            
            Log.Information($"[AfdianService] 成功获取赞助者列表，共 {allSponsors.Count} 人");
            return allSponsors;
        }
        catch (Exception ex)
        {
            Log.Error($"[AfdianService] 获取赞助者列表失败: {ex.Message}");
            
            // 失败时尝试返回缓存数据
            var cache = await LoadCacheAsync();
            return cache?.Sponsors ?? new List<AfdianSponsor>();
        }
    }
    
    /// <summary>
    /// 获取指定页的赞助者
    /// </summary>
    private async Task<List<AfdianSponsor>?> FetchSponsorsPageAsync(int page, string userId, string token)
    {
        try
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var paramsJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { page });
            var sign = CalculateSign(token, userId, paramsJson, ts);
            
            var requestData = new
            {
                user_id = userId,
                @params = paramsJson,
                ts,
                sign
            };
            
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var content = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(requestData),
                Encoding.UTF8,
                "application/json"
            );
            
            var response = await httpClient.PostAsync(ApiEndpoint, content);
            var responseText = await response.Content.ReadAsStringAsync();
            
            Log.Debug($"[AfdianService] API响应: {responseText}");
            
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(responseText);
            if (result == null)
            {
                return null;
            }
            
            var ec = result["ec"]?.Value<int>();
            if (ec != 200)
            {
                var em = result["em"]?.Value<string>();
                Log.Warning($"[AfdianService] API返回错误: ec={ec}, em={em}");
                return null;
            }
            
            var list = result["data"]?["list"];
            if (list == null)
            {
                return null;
            }
            
            var sponsors = new List<AfdianSponsor>();
            foreach (var item in list)
            {
                var user = item["user"];
                if (user == null) continue;
                
                sponsors.Add(new AfdianSponsor
                {
                    UserId = user["user_id"]?.Value<string>() ?? "",
                    Name = user["name"]?.Value<string>() ?? "匿名用户",
                    Avatar = user["avatar"]?.Value<string>() ?? "",
                    AllSumAmount = item["all_sum_amount"]?.Value<string>() ?? "0.00",
                    FirstPayTime = item["first_pay_time"]?.Value<long>() ?? 0,
                    LastPayTime = item["last_pay_time"]?.Value<long>() ?? 0
                });
            }
            
            return sponsors;
        }
        catch (Exception ex)
        {
            Log.Error($"[AfdianService] 获取第{page}页赞助者失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 计算签名
    /// sign = md5(token + params{params} + ts{ts} + user_id{user_id})
    /// </summary>
    private string CalculateSign(string token, string userId, string paramsJson, long ts)
    {
        var kvString = $"params{paramsJson}ts{ts}user_id{userId}";
        var signString = token + kvString;
        
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(signString));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
    
    /// <summary>
    /// 从缓存加载
    /// </summary>
    private async Task<AfdianSponsorCache?> LoadCacheAsync()
    {
        try
        {
            var cachePath = Path.Combine(_fileService.GetLauncherCachePath(), CacheFileName);
            if (!File.Exists(cachePath))
            {
                return null;
            }
            
            var json = await File.ReadAllTextAsync(cachePath);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<AfdianSponsorCache>(json);
        }
        catch (Exception ex)
        {
            Log.Warning($"[AfdianService] 加载缓存失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 保存到缓存
    /// </summary>
    private async Task SaveCacheAsync(AfdianSponsorCache cache)
    {
        try
        {
            var cachePath = Path.Combine(_fileService.GetLauncherCachePath(), CacheFileName);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(cache, Formatting.Indented);
            await File.WriteAllTextAsync(cachePath, json);
        }
        catch (Exception ex)
        {
            Log.Warning($"[AfdianService] 保存缓存失败: {ex.Message}");
        }
    }
}
