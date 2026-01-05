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
    
    // API Key（Base64 编码混淆，不是加密）
    private const string EncodedApiKey = "***REMOVED***"; 
    
    // 是否启用遥测（可以通过配置控制）
    private bool _isEnabled = true;
    
    /// <summary>
    /// 解码 API Key
    /// </summary>
    private static string GetApiKey()
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(EncodedApiKey));
        }
        catch
        {
            return string.Empty;
        }
    }

    public TelemetryService(HttpClient httpClient, ILogger<TelemetryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // 设置超时，避免阻塞启动
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        
        // 添加 API Key 请求头（解码后）
        var apiKey = GetApiKey();
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
    }

    /// <summary>
    /// 发送启动统计
    /// </summary>
    public async Task SendLaunchEventAsync()
    {
        if (!_isEnabled)
        {
            System.Diagnostics.Debug.WriteLine("[Telemetry] 遥测已禁用，跳过发送");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("[Telemetry] 开始准备遥测数据...");
            
            // 只发送一个空的 ping 请求，不包含任何用户信息
            var telemetryData = new
            {
                EventType = "AppLaunch",
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(telemetryData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            System.Diagnostics.Debug.WriteLine($"[Telemetry] 准备发送到: {TelemetryEndpoint}");
            System.Diagnostics.Debug.WriteLine($"[Telemetry] 数据内容: {json}");

            // 异步发送，不等待结果
            _ = Task.Run(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[Telemetry] 正在发送 HTTP 请求...");
                    var response = await _httpClient.PostAsync(TelemetryEndpoint, content);
                    
                    System.Diagnostics.Debug.WriteLine($"[Telemetry] 收到响应，状态码: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("遥测数据发送成功");
                        System.Diagnostics.Debug.WriteLine("[Telemetry] ✓ 遥测数据发送成功");
                    }
                    else
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning($"遥测数据发送失败: {response.StatusCode}");
                        System.Diagnostics.Debug.WriteLine($"[Telemetry] ✗ 发送失败: {response.StatusCode}");
                        System.Diagnostics.Debug.WriteLine($"[Telemetry] 响应内容: {responseBody}");
                    }
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning($"遥测数据发送超时: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Telemetry] ✗ 请求超时（5秒）: {ex.Message}");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning($"遥测数据发送网络错误: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Telemetry] ✗ 网络错误: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"遥测数据发送异常: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Telemetry] ✗ 未知异常: {ex.GetType().Name} - {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Telemetry] 堆栈跟踪: {ex.StackTrace}");
                }
            });
            
            System.Diagnostics.Debug.WriteLine("[Telemetry] 遥测任务已启动（后台执行）");
        }
        catch (Exception ex)
        {
            // 遥测失败不应影响应用启动
            _logger.LogWarning($"准备遥测数据失败: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[Telemetry] ✗ 准备数据失败: {ex.GetType().Name} - {ex.Message}");
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
