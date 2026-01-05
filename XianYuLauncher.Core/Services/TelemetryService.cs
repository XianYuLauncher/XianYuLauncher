using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 遥测统计服务
/// </summary>
public class TelemetryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelemetryService> _logger;
    
    // 统计服务端点
    private const string TelemetryEndpoint = "***REMOVED***";
    
    // 是否启用遥测（可以通过配置控制）
    private bool _isEnabled = true;

    public TelemetryService(HttpClient httpClient, ILogger<TelemetryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // 设置超时，避免阻塞启动
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// 发送启动统计
    /// </summary>
    public async Task SendLaunchEventAsync()
    {
        if (!_isEnabled)
        {
            return;
        }

        try
        {
            // 只发送一个空的 ping 请求，不包含任何用户信息
            var telemetryData = new
            {
                EventType = "AppLaunch",
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(telemetryData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 异步发送，不等待结果
            _ = Task.Run(async () =>
            {
                try
                {
                    var response = await _httpClient.PostAsync(TelemetryEndpoint, content);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("遥测数据发送成功");
                    }
                    else
                    {
                        _logger.LogWarning($"遥测数据发送失败: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"遥测数据发送异常: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            // 遥测失败不应影响应用启动
            _logger.LogWarning($"准备遥测数据失败: {ex.Message}");
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
