# 实现NeoForge版本列表获取与填充功能

## 一、需求分析

1. **核心目标**：实现从NeoForge官方API获取指定MC游戏版本对应的NeoForge版本列表，并填充到UI中
2. **API接口**：`https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml`
3. **数据格式**：XML格式，包含所有NeoForge版本信息
4. **版本匹配规则**：基于NeoForge版本号前缀（如20.2对应MC 1.20.2）
5. **排序要求**：版本号从新到旧排序

## 二、实现方案

### 1. 创建NeoForgeService类

**功能**：负责从NeoForge官方API获取版本列表并处理数据

**实现要点**：
- 接收Minecraft版本作为参数
- 发送HTTP请求获取XML数据
- 解析XML数据，提取所有版本号
- 根据Minecraft版本匹配对应的NeoForge版本
- 对版本列表进行排序（从新到旧）

### 2. 修改ModLoader选择ViewModel

**功能**：添加NeoForge版本列表获取逻辑

**实现要点**：
- 在`LoadModLoaderVersions`方法中添加NeoForge分支
- 调用NeoForgeService获取版本列表
- 将版本列表填充到UI控件中
- 处理异常情况，显示错误信息

### 3. 实现XML解析功能

**功能**：解析NeoForge API返回的XML数据

**实现要点**：
- 使用.NET内置的XML解析库（如XDocument）
- 提取`<versions>`节点下的所有`<version>`元素
- 将版本号存储到列表中

### 4. 实现版本匹配与排序

**功能**：匹配对应Minecraft版本的NeoForge版本，并排序

**实现要点**：
- 从Minecraft版本中提取主版本号（如从"1.20.2"提取"20.2"）
- 过滤出与主版本号匹配的NeoForge版本
- 使用Version类进行版本号比较和排序
- 反转排序结果，实现从新到旧排序

## 三、代码实现步骤

### 1. 创建NeoForgeService类

```csharp
public class NeoForgeService
{
    private readonly HttpClient _httpClient;

    public NeoForgeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<string>> GetNeoForgeVersionsAsync(string minecraftVersion)
    {
        try
        {
            // NeoForge API URL
            string url = "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
            
            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            
            // 确保响应成功
            response.EnsureSuccessStatusCode();
            
            // 读取响应内容
            string xml = await response.Content.ReadAsStringAsync();
            
            // 解析XML并提取版本列表
            List<string> allVersions = ParseNeoForgeVersionsFromXml(xml);
            
            // 匹配对应Minecraft版本的NeoForge版本
            List<string> matchedVersions = MatchNeoForgeVersions(allVersions, minecraftVersion);
            
            // 按版本号从新到旧排序
            List<string> sortedVersions = SortNeoForgeVersions(matchedVersions);
            
            return sortedVersions;
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"获取NeoForge版本列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"处理NeoForge版本列表时发生错误: {ex.Message}");
        }
    }

    private List<string> ParseNeoForgeVersionsFromXml(string xml)
    {
        var versionList = new List<string>();
        XDocument doc = XDocument.Parse(xml);
        
        // 提取所有版本号
        var versionElements = doc.Descendants("version");
        foreach (var element in versionElements)
        {
            versionList.Add(element.Value);
        }
        
        return versionList;
    }

    private List<string> MatchNeoForgeVersions(List<string> allVersions, string minecraftVersion)
    {
        // 从Minecraft版本中提取主版本号（如从"1.20.2"提取"20.2"）
        string mcMajorVersion = ExtractMinecraftMajorVersion(minecraftVersion);
        
        // 过滤出与主版本号匹配的NeoForge版本
        return allVersions.Where(v => v.StartsWith(mcMajorVersion + ".") || v.StartsWith(mcMajorVersion + "-")).ToList();
    }

    private string ExtractMinecraftMajorVersion(string minecraftVersion)
    {
        // 从Minecraft版本号中提取主版本号（如从"1.20.2"提取"20.2"）
        var parts = minecraftVersion.Split('.');
        if (parts.Length >= 3)
        {
            return $"{parts[1]}.{parts[2]}";
        }
        throw new Exception($"无效的Minecraft版本格式: {minecraftVersion}");
    }

    private List<string> SortNeoForgeVersions(List<string> versions)
    {
        // 按版本号从新到旧排序
        return versions.OrderByDescending(v => Version.Parse(ExtractVersionNumber(v))).ToList();
    }

    private string ExtractVersionNumber(string fullVersion)
    {
        // 从完整版本号中提取用于比较的版本号（如从"20.2.3-beta"提取"20.2.3"）
        string versionPart = fullVersion.Split('-')[0];
        return versionPart;
    }
}
```

### 2. 注册NeoForgeService

在`App.xaml.cs`的`ConfigureServices`方法中添加NeoForgeService的注册：

```csharp
// NeoForge Service
services.AddHttpClient<NeoForgeService>();
```

### 3. 修改ModLoader选择ViewModel

**添加NeoForgeService字段**：

```csharp
private readonly NeoForgeService _neoForgeService;
```

**在构造函数中初始化**：

```csharp
_neoForgeService = App.GetService<NeoForgeService>();
```

**修改LoadModLoaderVersions方法**：

```csharp
private async void LoadModLoaderVersions(string modLoader)
{
    // 清空版本列表
    AvailableModLoaderVersions.Clear();
    FabricVersionMap.Clear();
    SelectedModLoaderVersion = null;
    
    switch (modLoader)
    {
        case "Forge":
            AvailableModLoaderVersions.Add("47.2.0");
            AvailableModLoaderVersions.Add("47.1.0");
            AvailableModLoaderVersions.Add("47.0.0");
            break;
        case "Fabric":
            await LoadFabricVersionsAsync();
            break;
        case "NeoForge":
            await LoadNeoForgeVersionsAsync();
            break;
        case "Quilt":
            AvailableModLoaderVersions.Add("0.20.0");
            AvailableModLoaderVersions.Add("0.19.2");
            AvailableModLoaderVersions.Add("0.19.1");
            break;
    }
}
```

**添加LoadNeoForgeVersionsAsync方法**：

```csharp
private async Task LoadNeoForgeVersionsAsync()
{
    IsLoading = true;
    try
    {
        List<string> neoForgeVersions = await _neoForgeService.GetNeoForgeVersionsAsync(SelectedMinecraftVersion);
        
        // 将版本添加到列表中
        foreach (var version in neoForgeVersions)
        {
            AvailableModLoaderVersions.Add(version);
        }
        
        // 如果有版本，默认选择第一个
        if (AvailableModLoaderVersions.Count > 0)
        {
            SelectedModLoaderVersion = AvailableModLoaderVersions[0];
        }
    }
    catch (Exception ex)
    {
        await ShowMessageAsync($"获取NeoForge版本列表失败: {ex.Message}");
    }
    finally
    {
        IsLoading = false;
    }
}
```

## 四、日志输出

在关键步骤添加日志输出，便于调试：

1. **获取NeoForge版本列表前**：输出"正在获取NeoForge版本列表..."
2. **获取成功后**：输出"成功获取NeoForge版本列表，共X个版本"
3. **匹配版本后**：输出"匹配到X个对应Minecraft版本的NeoForge版本"
4. **排序后**：输出"版本列表已排序，共X个版本"
5. **填充到UI前**：输出"正在填充NeoForge版本列表到UI..."
6. **填充完成后**：输出"NeoForge版本列表填充完成"

## 五、异常处理

1. **HTTP请求异常**：显示"获取NeoForge版本列表失败，网络连接错误"
2. **XML解析异常**：显示"解析NeoForge版本列表失败，数据格式错误"
3. **版本匹配异常**：显示"未找到对应Minecraft版本的NeoForge版本"
4. **其他异常**：显示"获取NeoForge版本列表时发生未知错误"

## 六、测试计划

1. **单元测试**：测试版本匹配和排序逻辑
2. **集成测试**：测试API调用和数据处理流程
3. **UI测试**：测试版本列表在UI中的显示效果
4. **异常测试**：测试网络错误、数据格式错误等异常情况

## 七、预期效果

1. 当用户选择"NeoForge"作为ModLoader时，自动从NeoForge官方API获取对应Minecraft版本的NeoForge版本列表
2. 版本列表按从新到旧排序
3. 版本列表包含"NeoForge版本号"和"适配MC版本"信息
4. UI风格与Fabric版本列表保持一致
5. 支持异常情况的处理和错误提示
6. 包含详细的日志输出，便于调试和监控