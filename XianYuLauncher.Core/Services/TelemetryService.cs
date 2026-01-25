using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
    
    // 是否启用遥测
    private const string EnableTelemetryKey = "EnableTelemetry";

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
        var isEnabled = await _localSettingsService.ReadSettingAsync<bool?>(EnableTelemetryKey) ?? true;
        if (!isEnabled) return;
        
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

            await SendTelemetryDataAsync(firstLaunchData, onSuccess: async () =>
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

    private async Task SendTelemetryDataAsync<T>(T data, JsonSerializerOptions? options = null, Func<Task> onSuccess = null)
    {
        var json = options == null ? JsonSerializer.Serialize(data) : JsonSerializer.Serialize(data, options);
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
                    System.Diagnostics.Debug.WriteLine($"[Telemetry] 遥测数据 ({typeof(T).Name}) 发送成功");
                    if (onSuccess != null) await onSuccess();
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("遥测数据发送失败: {StatusCode} {Body}", response.StatusCode, responseBody);
                    if (string.IsNullOrWhiteSpace(responseBody))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Telemetry] 遥测数据发送失败: {response.StatusCode}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Telemetry] 遥测数据发送失败: {response.StatusCode} | {responseBody}");
                    }
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
        var isEnabled = await _localSettingsService.ReadSettingAsync<bool?>(EnableTelemetryKey) ?? true;
        if (!isEnabled) return;
        
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
    /// 记录游戏启动会话
    /// </summary>
    public async Task TrackGameSessionAsync(bool isSuccess, string mcVersion, string loaderType, string loaderVersion, int exitCode, double durationSeconds, int javaVersionMajor, int memoryAllocatedMb)
    {
        var isEnabled = await _localSettingsService.ReadSettingAsync<bool?>(EnableTelemetryKey) ?? true;
        if (!isEnabled) return;
        
        if (string.IsNullOrEmpty(TelemetryEndpoint)) return;

        try
        {
            var sanitizedMcVersion = isSuccess ? null : SanitizeString(mcVersion);
            var sanitizedLoaderType = isSuccess ? null : SanitizeString(loaderType);
            var sanitizedLoaderVersion = isSuccess ? null : SanitizeString(loaderVersion);

            var data = new
            {
                EventType = "GameSession",
                Timestamp = DateTime.UtcNow,
                Properties = new 
                {
                    IsSuccess = isSuccess,
                    MinecraftVersion = sanitizedMcVersion,
                    LoaderType = sanitizedLoaderType,
                    LoaderVersion = sanitizedLoaderVersion,
                    JavaVersionMajor = javaVersionMajor,
                    ExitCode = exitCode,
                    DurationSeconds = durationSeconds,
                    MemoryAllocatedMb = memoryAllocatedMb
                }
            };

            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            await SendTelemetryDataAsync(data, options);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Telemetry] 游戏会话统计发送失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清洗字符串，防止恶意Payload（仅允许字母数字和.-_）
    /// </summary>
    private string SanitizeString(string input, int maxLength = 32)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        
        // 1. 长度截断
        if (input.Length > maxLength)
        {
            input = input.Substring(0, maxLength);
        }
        
        // 2. 白名单过滤 (只允许字母、数字、点、横杠、下划线)
        // 任何不在此列表中的字符都会被移除
        return Regex.Replace(input, @"[^a-zA-Z0-9\.\-_]", ""); 
    }


}
