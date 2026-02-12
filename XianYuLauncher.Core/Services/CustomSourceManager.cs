using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 自定义下载源管理器
/// 负责自定义下载源的生命周期管理、配置持久化和验证
/// </summary>
public class CustomSourceManager
{
    private readonly DownloadSourceFactory _sourceFactory;
    private readonly ILogger<CustomSourceManager>? _logger;
    private readonly string _configFilePath;
    private CustomSourceConfig _config;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sourceFactory">下载源工厂</param>
    /// <param name="logger">日志记录器（可选）</param>
    public CustomSourceManager(
        DownloadSourceFactory sourceFactory,
        ILogger<CustomSourceManager>? logger = null)
    {
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        _logger = logger;
        _configFilePath = Path.Combine(AppEnvironment.SafeAppDataPath, "custom_sources.json");
        _config = new CustomSourceConfig();
    }

    /// <summary>
    /// 加载配置文件
    /// </summary>
    public async Task LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _logger?.LogInformation("配置文件不存在，创建默认配置: {ConfigPath}", _configFilePath);
                await CreateDefaultConfigurationAsync();
                return;
            }

            var json = await File.ReadAllTextAsync(_configFilePath);
            var config = JsonConvert.DeserializeObject<CustomSourceConfig>(json);

            if (config == null)
            {
                _logger?.LogError("配置文件解析失败，重置为默认配置");
                await CreateDefaultConfigurationAsync();
                return;
            }

            _config = config;
            _logger?.LogInformation("成功加载配置文件，共 {Count} 个自定义源", _config.Sources.Count);

            // 注册所有启用的自定义源到工厂
            foreach (var source in _config.Sources.Where(s => s.Enabled))
            {
                RegisterSourceToFactory(source);
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "配置文件 JSON 解析失败，重置为默认配置");
            await CreateDefaultConfigurationAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "加载配置文件时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    public async Task SaveConfigurationAsync()
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            await File.WriteAllTextAsync(_configFilePath, json);
            _logger?.LogInformation("配置文件已保存: {ConfigPath}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存配置文件时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 创建默认配置文件（包含两个禁用的模板示例）
    /// </summary>
    private async Task CreateDefaultConfigurationAsync()
    {
        _config = new CustomSourceConfig
        {
            Version = "1.0",
            Sources = new List<CustomSource>
            {
                new CustomSource
                {
                    Key = "example-bmclapi",
                    Name = "示例：BMCLAPI 镜像站",
                    Enabled = false,
                    BaseUrl = "https://bmclapi2.bangbang93.com",
                    Template = "bmclapi",
                    Priority = 100,
                    Overrides = null
                },
                new CustomSource
                {
                    Key = "example-mcim",
                    Name = "示例：MCIM 镜像站",
                    Enabled = false,
                    BaseUrl = "https://mod.mcimirror.top",
                    Template = "mcim",
                    Priority = 100,
                    Overrides = null
                }
            }
        };

        await SaveConfigurationAsync();
    }

    /// <summary>
    /// 将自定义源注册到下载源工厂
    /// </summary>
    /// <param name="source">自定义源配置</param>
    private void RegisterSourceToFactory(CustomSource source)
    {
        try
        {
            // 解析模板类型
            if (!Enum.TryParse<DownloadSourceTemplateType>(source.Template, true, out var templateType))
            {
                _logger?.LogWarning("无效的模板类型: {Template}，跳过注册源: {Name}", source.Template, source.Name);
                return;
            }

            // 获取模板
            var template = TemplateFactory.GetTemplate(templateType);

            // 创建自定义下载源实例
            var downloadSource = new CustomDownloadSource(
                source.Key,
                source.Name,
                source.BaseUrl,
                template,
                source.Overrides,
                source.Priority,
                _logger as ILogger<CustomDownloadSource>);

            // 注册到工厂
            _sourceFactory.RegisterSource(source.Key, downloadSource);
            _logger?.LogInformation("已注册自定义下载源: {Name} ({Key}), 优先级: {Priority}", 
                source.Name, source.Key, source.Priority);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "注册自定义下载源失败: {Name}", source.Name);
        }
    }

    /// <summary>
    /// 验证自定义源配置
    /// </summary>
    /// <param name="name">源名称</param>
    /// <param name="baseUrl">基础 URL</param>
    /// <param name="template">模板类型</param>
    /// <param name="priority">优先级</param>
    /// <param name="excludeKey">排除的 key（用于更新时跳过自身）</param>
    /// <returns>验证结果</returns>
    private Result ValidateSource(
        string name, 
        string baseUrl, 
        DownloadSourceTemplateType template,
        int priority,
        string? excludeKey = null)
    {
        // 验证名称非空
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Fail("源名称不能为空");
        }

        // 验证名称不包含非法字符
        var invalidChars = Path.GetInvalidFileNameChars();
        if (name.Any(c => invalidChars.Contains(c)))
        {
            return Result.Fail("源名称包含非法字符");
        }

        // 验证名称不与现有源冲突（排除自身）
        var existingSource = _config.Sources.FirstOrDefault(s => 
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && 
            s.Key != excludeKey);
        if (existingSource != null)
        {
            return Result.Fail($"源名称 '{name}' 已存在");
        }

        // 验证 Base URL 格式
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return Result.Fail("Base URL 不能为空");
        }

        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail("Base URL 必须以 http:// 或 https:// 开头");
        }

        // 验证 URL 不包含查询参数或片段
        if (baseUrl.Contains('?') || baseUrl.Contains('#'))
        {
            return Result.Fail("Base URL 不能包含查询参数或片段");
        }

        // 验证模板类型有效
        if (!Enum.IsDefined(typeof(DownloadSourceTemplateType), template))
        {
            return Result.Fail("无效的模板类型");
        }

        // 验证优先级为正整数
        if (priority <= 0)
        {
            return Result.Fail("优先级必须是正整数");
        }

        return Result.Ok();
    }

    /// <summary>
    /// 添加自定义下载源
    /// </summary>
    /// <param name="name">源名称</param>
    /// <param name="baseUrl">基础 URL</param>
    /// <param name="template">模板类型</param>
    /// <param name="enabled">是否启用</param>
    /// <param name="priority">优先级</param>
    /// <returns>添加结果</returns>
    public async Task<Result<CustomSource>> AddSourceAsync(
        string name,
        string baseUrl,
        DownloadSourceTemplateType template,
        bool enabled = true,
        int priority = 100)
    {
        try
        {
            // 验证输入
            var validationResult = ValidateSource(name, baseUrl, template, priority);
            if (!validationResult.Success)
            {
                return Result<CustomSource>.Fail(validationResult.ErrorMessage!);
            }

            // 生成唯一 key
            var key = $"custom-{Guid.NewGuid():N}";

            // 创建自定义源对象
            var source = new CustomSource
            {
                Key = key,
                Name = name,
                Enabled = enabled,
                BaseUrl = baseUrl.TrimEnd('/'),
                Template = template.ToString().ToLowerInvariant(),
                Priority = priority,
                Overrides = null
            };

            // 添加到配置
            _config.Sources.Add(source);

            // 如果启用，注册到工厂
            if (enabled)
            {
                RegisterSourceToFactory(source);
            }

            // 保存配置
            await SaveConfigurationAsync();

            _logger?.LogInformation("成功添加自定义下载源: {Name} ({Key})", name, key);
            return Result<CustomSource>.Ok(source);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "添加自定义下载源失败: {Name}", name);
            return Result<CustomSource>.Fail($"添加失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新自定义下载源
    /// </summary>
    /// <param name="key">源标识</param>
    /// <param name="name">新名称</param>
    /// <param name="baseUrl">新基础 URL</param>
    /// <param name="template">新模板类型</param>
    /// <param name="enabled">是否启用</param>
    /// <param name="priority">新优先级</param>
    /// <returns>更新结果</returns>
    public async Task<Result> UpdateSourceAsync(
        string key,
        string name,
        string baseUrl,
        DownloadSourceTemplateType template,
        bool enabled,
        int priority)
    {
        try
        {
            // 查找源
            var source = _config.Sources.FirstOrDefault(s => s.Key == key);
            if (source == null)
            {
                return Result.Fail($"未找到标识为 '{key}' 的自定义源");
            }

            // 验证输入（排除自身）
            var validationResult = ValidateSource(name, baseUrl, template, priority, key);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            // 如果之前启用，先注销
            if (source.Enabled)
            {
                _sourceFactory.UnregisterSource(key);
            }

            // 更新配置
            source.Name = name;
            source.BaseUrl = baseUrl.TrimEnd('/');
            source.Template = template.ToString().ToLowerInvariant();
            source.Enabled = enabled;
            source.Priority = priority;

            // 如果启用，重新注册
            if (enabled)
            {
                RegisterSourceToFactory(source);
            }

            // 保存配置
            await SaveConfigurationAsync();

            _logger?.LogInformation("成功更新自定义下载源: {Name} ({Key})", name, key);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "更新自定义下载源失败: {Key}", key);
            return Result.Fail($"更新失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除自定义下载源
    /// </summary>
    /// <param name="key">源标识</param>
    /// <returns>删除结果</returns>
    public async Task<Result> DeleteSourceAsync(string key)
    {
        try
        {
            // 验证不是内置源
            if (key == "official" || key == "bmclapi" || key == "mcim")
            {
                return Result.Fail("不能删除内置下载源");
            }

            // 查找源
            var source = _config.Sources.FirstOrDefault(s => s.Key == key);
            if (source == null)
            {
                return Result.Fail($"未找到标识为 '{key}' 的自定义源");
            }

            // 从工厂注销
            _sourceFactory.UnregisterSource(key);

            // 从配置移除
            _config.Sources.Remove(source);

            // 保存配置
            await SaveConfigurationAsync();

            _logger?.LogInformation("成功删除自定义下载源: {Name} ({Key})", source.Name, key);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "删除自定义下载源失败: {Key}", key);
            return Result.Fail($"删除失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 切换下载源启用状态
    /// </summary>
    /// <param name="key">源标识</param>
    /// <param name="enabled">是否启用</param>
    /// <returns>切换结果</returns>
    public async Task<Result> ToggleSourceAsync(string key, bool enabled)
    {
        try
        {
            // 查找源
            var source = _config.Sources.FirstOrDefault(s => s.Key == key);
            if (source == null)
            {
                return Result.Fail($"未找到标识为 '{key}' 的自定义源");
            }

            // 如果状态相同，无需操作
            if (source.Enabled == enabled)
            {
                return Result.Ok();
            }

            // 更新状态
            source.Enabled = enabled;

            if (enabled)
            {
                // 启用：注册到工厂
                RegisterSourceToFactory(source);
            }
            else
            {
                // 禁用：从工厂注销
                _sourceFactory.UnregisterSource(key);
            }

            // 保存配置
            await SaveConfigurationAsync();

            _logger?.LogInformation("成功切换自定义下载源状态: {Name} ({Key}) -> {Enabled}", 
                source.Name, key, enabled);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "切换自定义下载源状态失败: {Key}", key);
            return Result.Fail($"切换失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有自定义下载源
    /// </summary>
    /// <returns>自定义源列表</returns>
    public IReadOnlyList<CustomSource> GetAllSources()
    {
        _logger?.LogInformation("GetAllSources 被调用，当前配置中有 {Count} 个源", _config.Sources.Count);
        foreach (var source in _config.Sources)
        {
            _logger?.LogInformation("  - {Name} (Key={Key}, Enabled={Enabled})", source.Name, source.Key, source.Enabled);
        }
        return _config.Sources.AsReadOnly();
    }

    /// <summary>
    /// 导出配置
    /// </summary>
    /// <param name="targetPath">目标文件路径</param>
    /// <returns>导出结果</returns>
    public async Task<Result<string>> ExportConfigurationAsync(string targetPath)
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            await File.WriteAllTextAsync(targetPath, json);

            _logger?.LogInformation("成功导出配置到: {TargetPath}", targetPath);
            return Result<string>.Ok(targetPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "导出配置失败");
            return Result<string>.Fail($"导出失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 导入配置
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="strategy">冲突解决策略</param>
    /// <returns>导入结果</returns>
    public async Task<Result> ImportConfigurationAsync(
        string sourcePath,
        ConflictResolutionStrategy strategy)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                return Result.Fail("配置文件不存在");
            }

            var json = await File.ReadAllTextAsync(sourcePath);
            var importConfig = JsonConvert.DeserializeObject<CustomSourceConfig>(json);

            if (importConfig == null)
            {
                return Result.Fail("配置文件格式无效");
            }

            // 检查版本兼容性
            if (importConfig.Version != _config.Version)
            {
                _logger?.LogWarning("配置文件版本不匹配: {ImportVersion} vs {CurrentVersion}", 
                    importConfig.Version, _config.Version);
            }

            var importedCount = 0;
            var skippedCount = 0;

            foreach (var importSource in importConfig.Sources)
            {
                var existingSource = _config.Sources.FirstOrDefault(s => s.Key == importSource.Key);

                if (existingSource != null)
                {
                    // 处理冲突
                    switch (strategy)
                    {
                        case ConflictResolutionStrategy.Skip:
                            skippedCount++;
                            continue;

                        case ConflictResolutionStrategy.Overwrite:
                            _config.Sources.Remove(existingSource);
                            _config.Sources.Add(importSource);
                            importedCount++;
                            break;

                        case ConflictResolutionStrategy.Rename:
                            importSource.Key = $"custom-{Guid.NewGuid():N}";
                            importSource.Name = $"{importSource.Name} (导入)";
                            _config.Sources.Add(importSource);
                            importedCount++;
                            break;
                    }
                }
                else
                {
                    _config.Sources.Add(importSource);
                    importedCount++;
                }

                // 如果启用，注册到工厂
                if (importSource.Enabled)
                {
                    RegisterSourceToFactory(importSource);
                }
            }

            // 保存配置
            await SaveConfigurationAsync();

            _logger?.LogInformation("成功导入配置: 导入 {ImportedCount} 个，跳过 {SkippedCount} 个", 
                importedCount, skippedCount);
            return Result.Ok();
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "导入配置失败: JSON 解析错误");
            return Result.Fail("配置文件格式无效");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "导入配置失败");
            return Result.Fail($"导入失败: {ex.Message}");
        }
    }
}
