using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 遥测统计服务
/// </summary>
public class TelemetryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelemetryService> _logger;
    private readonly ILocalSettingsService _localSettingsService;
    
    // 从配置文件读取
    private static string TelemetryEndpoint => SecretsService.Config.Telemetry.Endpoint;
    private static string ApiKey => SecretsService.Config.Telemetry.ApiKey;
    
    // 是否启用遥测（可以通过配置控制）
    private bool _isEnabled = true;

    public TelemetryService(HttpClient httpClient, ILogger<TelemetryService> logger, ILocalSettingsService localSettingsService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _localSettingsService = localSettingsService;
        
        // 设置超时，避免阻塞启动 (用户请求延长等待)
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // 添加 API Key 请求头
        var apiKey = ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
    }

    /// <summary>
    /// 检查并发送首次启动事件
    /// </summary>
    public async Task CheckAndSendFirstLaunchAsync()
    {
        if (!_isEnabled) return;
        if (string.IsNullOrEmpty(TelemetryEndpoint)) return;

        try
        {
            // 检查是否已经发送过
            var isSent = await _localSettingsService.ReadSettingAsync<bool>("IsFirstLaunchTelemetrySent");
            if (isSent) return;

            var firstLaunchData = new
            {
                EventType = "FirstLaunch",
                Timestamp = DateTime.UtcNow
            };

            await SendTelemetryDataAsync(firstLaunchData, async () => 
            {
                // 发送成功后标记为已发送
                await _localSettingsService.SaveSettingAsync("IsFirstLaunchTelemetrySent", true);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Telemetry] 首次启动遥测检查失败: {ex.Message}");
        }
    }

    private async Task SendTelemetryDataAsync<T>(T data, Func<Task> onSuccess = null)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 异步发送，不等待
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await _httpClient.PostAsync(TelemetryEndpoint, content);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"遥测数据 ({typeof(T).Name}) 发送成功");
                    if (onSuccess != null) await onSuccess();
                }
                else
                {
                    _logger.LogWarning($"遥测数据发送失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Telemetry] 发送异常: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 发送启动统计
    /// </summary>
    public async Task SendLaunchEventAsync()
    {
        if (!_isEnabled) return;
        if (string.IsNullOrEmpty(TelemetryEndpoint)) return;

        try
        {
            var telemetryData = new
            {
                EventType = "AppLaunch",
                Timestamp = DateTime.UtcNow
            };
            
            await SendTelemetryDataAsync(telemetryData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Telemetry] 启动统计发送失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置是否启用遥测
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
    }
}
