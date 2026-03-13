using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 自定义下载源管理器
/// 负责自定义下载源的生命周期管理、配置持久化和验证
/// 新架构：每个源一个独立 JSON 文件，自动扫描文件夹
/// </summary>
public class CustomSourceManager
{
    private readonly DownloadSourceFactory _sourceFactory;
    private readonly ILogger<CustomSourceManager>? _logger;
    private readonly string _sourcesDirectory;
    private readonly Dictionary<string, CustomSource> _sources = new();

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
        _sourcesDirectory = Path.Combine(AppEnvironment.SafeAppDataPath, "CustomSources");
        
        // 确保目录存在
        if (!Directory.Exists(_sourcesDirectory))
        {
            Directory.CreateDirectory(_sourcesDirectory);
            _logger?.LogInformation("创建自定义源目录: {Directory}", _sourcesDirectory);
        }
    }

    /// <summary>
    /// 加载所有自定义源配置（扫描文件夹）
    /// </summary>
    public async Task LoadConfigurationAsync()
    {
        try
        {
            _sources.Clear();
            
            // 扫描所有 .json 文件
            var jsonFiles = Directory.GetFiles(_sourcesDirectory, "*.json", SearchOption.TopDirectoryOnly);
            
            if (jsonFiles.Length == 0)
            {
                _logger?.LogInformation("未找到自定义源配置文件");
                await CreateExampleConfigurationsAsync();
                return;
            }

            _logger?.LogInformation("开始扫描自定义源配置文件，共 {Count} 个", jsonFiles.Length);

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var json = await File.ReadAllTextAsync(filePath);
                    var source = JsonConvert.DeserializeObject<CustomSource>(json);

                    if (source == null)
                    {
                        _logger?.LogWarning("配置文件解析失败，跳过: {FileName}", fileName);
                        continue;
                    }

                    // 使用文件名作为 key（覆盖 JSON 中的 key）
                    source.Key = fileName;

                    // 验证必填字段
                    if (string.IsNullOrWhiteSpace(source.Name) || 
                        string.IsNullOrWhiteSpace(source.BaseUrl) || 
                        string.IsNullOrWhiteSpace(source.Template))
                    {
                        _logger?.LogWarning("配置文件缺少必填字段，跳过: {FileName}", fileName);
                        continue;
                    }

                    // 添加到字典
                    _sources[fileName] = source;

                    // 如果启用，注册到工厂
                    if (source.Enabled)
                    {
                        RegisterSourceToFactory(source);
                    }

                    _logger?.LogInformation("成功加载自定义源: {Name} ({Key}), 启用: {Enabled}", 
                        source.Name, fileName, source.Enabled);
                }
                catch (JsonException ex)
                {
                    _logger?.LogError(ex, "JSON 解析失败，跳过文件: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "加载配置文件失败，跳过: {FilePath}", filePath);
                }
            }

            _logger?.LogInformation("自定义源加载完成，共 {Count} 个有效源", _sources.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "扫描自定义源目录时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 保存单个源配置到文件
    /// </summary>
    private async Task SaveSourceAsync(string key, CustomSource source)
    {
        try
        {
            var filePath = Path.Combine(_sourcesDirectory, $"{key}.json");
            var json = JsonConvert.SerializeObject(source, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json);
            _logger?.LogInformation("配置文件已保存: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存配置文件失败: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// 删除源配置文件
    /// </summary>
    private void DeleteSourceFile(string key)
    {
        try
        {
            var filePath = Path.Combine(_sourcesDirectory, $"{key}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger?.LogInformation("配置文件已删除: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "删除配置文件失败: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// 创建示例配置文件（禁用状态）
    /// </summary>
    private async Task CreateExampleConfigurationsAsync()
    {
        try
        {
            // 示例 1: 官方资源镜像
            var officialExample = new CustomSource
            {
                Key = "example-official",
                Name = "示例：官方资源镜像站",
                Enabled = false,
                BaseUrl = "https://bmclapi2.bangbang93.com",
                Template = "official",
                Priority = 100,
                Overrides = null
            };
            await SaveSourceAsync("example-official", officialExample);

            // 示例 2: 社区资源镜像
            var communityExample = new CustomSource
            {
                Key = "example-community",
                Name = "示例：社区资源镜像站",
                Enabled = false,
                BaseUrl = "https://mod.mcimirror.top",
                Template = "community",
                Priority = 100,
                Overrides = null
            };
            await SaveSourceAsync("example-community", communityExample);

            _logger?.LogInformation("已创建示例配置文件");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "创建示例配置文件失败");
        }
    }

    /// <summary>
    /// 将自定义源注册到下载源工厂
    /// </summary>
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

        // 验证名称不与现有源冲突（排除自身）
        var existingSource = _sources.Values.FirstOrDefault(s => 
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
    /// 生成唯一的文件名（key）
    /// </summary>
    private string GenerateUniqueKey(string baseName)
    {
        // 移除非法字符
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = string.Join("_", baseName.Split(invalidChars));
        
        // 转换为小写并替换空格
        safeName = safeName.ToLowerInvariant().Replace(" ", "-");
        
        // 如果不存在冲突，直接返回
        if (!_sources.ContainsKey(safeName))
        {
            return safeName;
        }
        
        // 存在冲突，添加数字后缀
        var counter = 1;
        while (_sources.ContainsKey($"{safeName}-{counter}"))
        {
            counter++;
        }
        
        return $"{safeName}-{counter}";
    }

    /// <summary>
    /// 添加自定义下载源
    /// </summary>
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

            // 生成唯一 key（基于名称）
            var key = GenerateUniqueKey(name);

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

            // 保存到文件
            await SaveSourceAsync(key, source);

            // 添加到内存字典
            _sources[key] = source;

            // 如果启用，注册到工厂
            if (enabled)
            {
                RegisterSourceToFactory(source);
            }

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
            if (!_sources.TryGetValue(key, out var source))
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

            // 保存到文件
            await SaveSourceAsync(key, source);

            // 如果启用，重新注册
            if (enabled)
            {
                RegisterSourceToFactory(source);
            }

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
    public async Task<Result> DeleteSourceAsync(string key)
    {
        try
        {
            // 查找源
            if (!_sources.TryGetValue(key, out var source))
            {
                return Result.Fail($"未找到标识为 '{key}' 的自定义源");
            }

            // 从工厂注销
            _sourceFactory.UnregisterSource(key);

            // 删除文件
            DeleteSourceFile(key);

            // 从内存移除
            _sources.Remove(key);

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
    public async Task<Result> ToggleSourceAsync(string key, bool enabled)
    {
        try
        {
            // 查找源
            if (!_sources.TryGetValue(key, out var source))
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

            // 保存到文件
            await SaveSourceAsync(key, source);

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
    public IReadOnlyList<CustomSource> GetAllSources()
    {
        return _sources.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// 导入单个源配置文件
    /// </summary>
    public async Task<Result<CustomSource>> ImportSourceAsync(string sourcePath)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                return Result<CustomSource>.Fail("配置文件不存在");
            }

            var json = await File.ReadAllTextAsync(sourcePath);
            var source = JsonConvert.DeserializeObject<CustomSource>(json);

            if (source == null)
            {
                return Result<CustomSource>.Fail("配置文件格式无效");
            }

            // 验证必填字段
            if (string.IsNullOrWhiteSpace(source.Name) || 
                string.IsNullOrWhiteSpace(source.BaseUrl) || 
                string.IsNullOrWhiteSpace(source.Template))
            {
                return Result<CustomSource>.Fail("配置文件缺少必填字段");
            }

            // 解析模板类型
            if (!Enum.TryParse<DownloadSourceTemplateType>(source.Template, true, out var templateType))
            {
                return Result<CustomSource>.Fail($"无效的模板类型: {source.Template}");
            }

            // 生成新的 key（避免冲突）
            var key = GenerateUniqueKey(source.Name);
            source.Key = key;

            // 如果名称冲突，添加后缀
            if (_sources.Values.Any(s => s.Name.Equals(source.Name, StringComparison.OrdinalIgnoreCase)))
            {
                source.Name = $"{source.Name} (导入)";
            }

            // 保存到文件
            await SaveSourceAsync(key, source);

            // 添加到内存
            _sources[key] = source;

            // 如果启用，注册到工厂
            if (source.Enabled)
            {
                RegisterSourceToFactory(source);
            }

            _logger?.LogInformation("成功导入自定义源: {Name} ({Key})", source.Name, key);
            return Result<CustomSource>.Ok(source);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "导入配置失败: JSON 解析错误");
            return Result<CustomSource>.Fail("配置文件格式无效");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "导入配置失败");
            return Result<CustomSource>.Fail($"导入失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 导出单个源配置
    /// </summary>
    public async Task<Result<string>> ExportSourceAsync(string key, string targetPath)
    {
        try
        {
            if (!_sources.TryGetValue(key, out var source))
            {
                return Result<string>.Fail($"未找到标识为 '{key}' 的自定义源");
            }

            // 确保目录存在
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(source, Formatting.Indented);
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
    /// 刷新配置（重新扫描文件夹）
    /// </summary>
    public async Task RefreshAsync()
    {
        await LoadConfigurationAsync();
    }

    /// <summary>
    /// 获取配置文件夹路径
    /// </summary>
    public string GetSourcesDirectory() => _sourcesDirectory;
}
