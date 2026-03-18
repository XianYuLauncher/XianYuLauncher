using System.Text.Json;
using Serilog;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Services;

/// <summary>
/// 从本地静态清单读取赞助者数据，不发起网络请求。
/// </summary>
public class LocalAfdianService : IAfdianService
{
    private const string SponsorsManifestRelativePath = "Assets/Data/afdian-sponsors.json";

    public async Task<List<AfdianSponsor>> GetSponsorsAsync()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var manifestPath = Path.Combine(baseDir, SponsorsManifestRelativePath);

            if (!File.Exists(manifestPath))
            {
                Log.Information("[LocalAfdianService] 本地赞助者清单不存在: {Path}", manifestPath);
                return new List<AfdianSponsor>();
            }

            var json = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<AfdianSponsorsManifest>(json, JsonOptions);
            if (manifest?.Sponsors == null || manifest.Sponsors.Count == 0)
            {
                return new List<AfdianSponsor>();
            }

            return manifest.Sponsors
                .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                .Select(s => new AfdianSponsor
                {
                    Name = s.Name?.Trim() ?? string.Empty,
                    Avatar = s.Avatar ?? string.Empty,
                    AllSumAmount = string.IsNullOrWhiteSpace(s.AllSumAmount) ? "0.00" : s.AllSumAmount!,
                    UserId = string.Empty,
                    FirstPayTime = 0,
                    LastPayTime = 0
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[LocalAfdianService] 读取本地赞助者清单失败");
            return new List<AfdianSponsor>();
        }
    }

    public Task<List<AfdianSponsor>> RefreshSponsorsAsync()
    {
        return GetSponsorsAsync();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class AfdianSponsorsManifest
    {
        public List<ManifestSponsor> Sponsors { get; set; } = new();
    }

    private sealed class ManifestSponsor
    {
        public string? Name { get; set; }
        public string? Avatar { get; set; }
        public string? AllSumAmount { get; set; }
    }
}
