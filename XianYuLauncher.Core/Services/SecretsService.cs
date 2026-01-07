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
    private static readonly object _lock = new();

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
            // 尝试从应用目录读取 secrets.json
            var appDir = AppContext.BaseDirectory;
            var secretsPath = Path.Combine(appDir, "secrets.json");
            
            if (File.Exists(secretsPath))
            {
                var json = File.ReadAllText(secretsPath);
                var config = JsonSerializer.Deserialize<SecretsConfig>(json);
                if (config != null)
                {
                    System.Diagnostics.Debug.WriteLine("[SecretsService] 成功加载 secrets.json");
                    return config;
                }
            }
            
            System.Diagnostics.Debug.WriteLine("[SecretsService] secrets.json 不存在，使用空配置");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecretsService] 加载配置失败: {ex.Message}");
        }
        
        // 返回空配置
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
