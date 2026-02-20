using System;
using System.IO;
using System.Text.Json;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 敏感配置读取服务
/// </summary>
public static class SecretsService
{
    private static SecretsConfig? _config;
    private static readonly Lock _lock = new();

    /// <summary>
    /// 获取配置
    /// </summary>
    public static SecretsConfig Config
    {
        get
        {
            if (_config == null)
            {
                lock (_lock)
                {
                    _config ??= LoadConfig();
                }
            }
            return _config;
        }
    }

    private static SecretsConfig LoadConfig()
    {
        try
        {
            var appDir = AppContext.BaseDirectory;
            
            // 1. 尝试加载处理后的配置文件
            var encPath = Path.Combine(appDir, "secrets.enc");
            if (File.Exists(encPath))
            {
                try
                {
                    var data = File.ReadAllText(encPath);
                    var decoded = XianYuLauncher.Core.Helpers.SecretProtector.Decrypt(data);
                    var config = JsonSerializer.Deserialize<SecretsConfig>(decoded);
                    if (config != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[SecretsService] 配置加载成功");
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SecretsService] 配置处理失败: {ex.Message}");
                }
            }
            
            // 2. 降级：尝试读取原始配置文件（开发模式）
            var secretsPath = Path.Combine(appDir, "secrets.json");
            if (File.Exists(secretsPath))
            {
                var json = File.ReadAllText(secretsPath);
                var config = JsonSerializer.Deserialize<SecretsConfig>(json);
                if (config != null)
                {
                    System.Diagnostics.Debug.WriteLine("[SecretsService] 使用开发配置");
                    return config;
                }
            }
            
            System.Diagnostics.Debug.WriteLine("[SecretsService] 未找到配置文件");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecretsService] 配置加载异常: {ex.Message}");
        }
        
        return new SecretsConfig();
    }

    /// <summary>
    /// 重新加载配置
    /// </summary>
    public static void Reload()
    {
        lock (_lock)
        {
            _config = null;
        }
    }
}

/// <summary>
/// 敏感配置模型
/// </summary>
public class SecretsConfig
{
    public MicrosoftAuthConfig MicrosoftAuth { get; set; } = new();
    public TelemetryConfig Telemetry { get; set; } = new();
    public AiAnalysisConfig AiAnalysis { get; set; } = new();
    public CurseForgeConfig CurseForge { get; set; } = new();
    public AfdianConfig Afdian { get; set; } = new();
}

public class MicrosoftAuthConfig
{
    public string ClientId { get; set; } = string.Empty;
}

public class TelemetryConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class AiAnalysisConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "Qwen/Qwen3-14B";
    public string BaseUrl { get; set; } = "https://api.siliconflow.cn/v1/chat/completions";
}

public class CurseForgeConfig
{
    public string ApiKey { get; set; } = string.Empty;
}

public class AfdianConfig
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
