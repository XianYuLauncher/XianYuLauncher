# 实现NeoForge安装器下载、拆包和JSON合并功能

## 一、需求分析

1. **核心目标**：
   - 实现NeoForge安装器下载
   - 实现核心依赖预处理
   - 实现原版与NeoForge版本JSON的合并

2. **前置条件**：
   - 已完成NeoForge版本列表的获取与展示
   - Fabric加载器的下载/版本JSON处理逻辑已成熟

3. **关键约束**：
   - 严格遵循NeoForge官方适配wiki流程
   - 复用Fabric已有的文件下载工具类、JSON解析工具类和进度回调逻辑
   - 仅实现下载+拆包+JSON合并，暂不执行install_profile.json中的processors处理器和BINPATCH补丁

## 二、实现方案

### 1. 整体架构设计

**核心类和方法**：
- `MinecraftVersionService.DownloadModLoaderVersionAsync`：主入口方法，根据ModLoader类型执行不同的下载逻辑
- `MinecraftVersionService.DownloadNeoForgeVersionAsync`：NeoForge特定的下载逻辑
- `NeoForgeInstallerHelper`：辅助类，处理NeoForge安装器的拆包和JSON合并

**工作流程**：
1. 调用`DownloadModLoaderVersionAsync`，传入"NeoForge"作为ModLoader类型
2. 调用`DownloadNeoForgeVersionAsync`，执行NeoForge特定的下载逻辑
3. 下载原版Minecraft核心文件
4. 下载NeoForge安装器
5. 拆包NeoForge安装器
6. 合并原版和NeoForge版本JSON
7. 保存合并后的JSON文件

### 2. 详细实现步骤

#### 2.1 前置校验与准备

**复用现有逻辑**：
- 复用`GetVersionInfoAsync`方法获取原版Minecraft版本信息
- 复用`DownloadVersionAsync`方法下载原版核心文件
- 复用`GetVersionInfoJsonAsync`方法获取原版version.json

**实现逻辑**：
```csharp
// 1. 获取原版Minecraft版本信息
var originalVersionInfo = await GetVersionInfoAsync(minecraftVersionId, minecraftDirectory);

// 2. 下载原版核心文件
string originalVersionDirectory = Path.Combine(versionsDirectory, minecraftVersionId);
await DownloadVersionAsync(minecraftVersionId, originalVersionDirectory);

// 3. 校验原版文件完整性
var clientDownload = originalVersionInfo.Downloads.Client;
var jarPath = Path.Combine(originalVersionDirectory, $"{minecraftVersionId}.jar");
// 验证JAR文件的SHA1哈希（复用现有逻辑）
```

#### 2.2 NeoForge安装器下载

**实现逻辑**：
```csharp
// 1. 拼接下载地址
string neoforgeDownloadUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoforgeVersion}/neoforge-{neoforgeVersion}-installer.jar";

// 2. 下载installer.jar到缓存目录
string cacheDirectory = Path.Combine(_fileService.GetAppDataPath(), "cache", "neoforge");
string installerPath = Path.Combine(cacheDirectory, $"neoforge-{neoforgeVersion}-installer.jar");
await DownloadFileAsync(neoforgeDownloadUrl, installerPath, progressCallback);

// 3. 支持下载进度显示（复用现有进度回调逻辑）
```

#### 2.3 installer.jar拆包处理

**实现逻辑**：
```csharp
// 1. 将installer.jar视为ZIP包解压
string extractDirectory = Path.Combine(cacheDirectory, $"neoforge-{neoforgeVersion}");
using (var archive = ZipFile.OpenRead(installerPath))
{
    // 2. 提取关键文件：install_profile.json、version.json、data/client.lzma
    ExtractFileFromArchive(archive, "install_profile.json", extractDirectory);
    ExtractFileFromArchive(archive, "version.json", extractDirectory);
    ExtractFileFromArchive(archive, "data/client.lzma", Path.Combine(extractDirectory, "data"));
}

// 3. 解析install_profile.json
string installProfilePath = Path.Combine(extractDirectory, "install_profile.json");
var installProfile = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(installProfilePath));

// 4. 验证MC_SLIM字段与已下载原版核心文件的版本匹配性
string mcSlimField = installProfile.MC_SLIM.ToString();
// 验证逻辑...
```

#### 2.4 版本JSON合并

**实现逻辑**：
```csharp
// 1. 获取原版version.json
string originalJsonPath = Path.Combine(originalVersionDirectory, $"{minecraftVersionId}.json");
string originalJsonContent = File.ReadAllText(originalJsonPath);
var originalJson = JsonConvert.DeserializeObject<VersionInfo>(originalJsonContent);

// 2. 获取NeoForge version.json
string neoforgeJsonPath = Path.Combine(extractDirectory, "version.json");
string neoforgeJsonContent = File.ReadAllText(neoforgeJsonPath);
var neoforgeJson = JsonConvert.DeserializeObject<VersionInfo>(neoforgeJsonContent);

// 3. 合并两个JSON文件
var mergedJson = MergeVersionJson(originalJson, neoforgeJson);

// 4. 保存合并后的JSON文件
string neoforgeVersionId = $"neoforge-{minecraftVersionId}-{neoforgeVersion}";
string neoforgeVersionDirectory = Path.Combine(versionsDirectory, neoforgeVersionId);
Directory.CreateDirectory(neoforgeVersionDirectory);
string mergedJsonPath = Path.Combine(neoforgeVersionDirectory, $"{neoforgeVersionId}.json");
File.WriteAllText(mergedJsonPath, JsonConvert.SerializeObject(mergedJson, Formatting.Indented));
```

### 3. 版本JSON合并规则

**合并规则**：

| 字段 | 处理方式 | 说明 |
|------|----------|------|
| id | 替换 | 使用NeoForge版本ID（如neoforge-1.20.2-20.2.80） |
| type | 替换 | 使用NeoForge的类型 |
| time | 替换 | 使用NeoForge的时间 |
| releaseTime | 替换 | 使用NeoForge的发布时间 |
| inheritsFrom | 替换 | 指向原版Minecraft版本ID |
| mainClass | 替换 | 使用NeoForge的主类（如net.neoforged.neolauncher.NeoLauncher） |
| arguments | 替换 | 使用NeoForge的JVM参数和游戏参数 |
| libraries | 追加 | 合并原版和NeoForge的依赖库 |
| assets | 保留 | 使用原版的assets |
| assetIndex | 保留 | 使用原版的assetIndex |
| downloads | 保留 | 使用原版的downloads |
| logging | 保留 | 使用原版的logging |
| javaVersion | 替换 | 使用NeoForge的Java版本要求 |

**合并逻辑实现**：
```csharp
private VersionInfo MergeVersionJson(VersionInfo original, VersionInfo neoforge)
{
    var merged = new VersionInfo
    {
        Id = neoforge.Id,
        Type = neoforge.Type,
        Time = neoforge.Time,
        ReleaseTime = neoforge.ReleaseTime,
        InheritsFrom = original.Id,
        MainClass = neoforge.MainClass,
        Arguments = neoforge.Arguments,
        Libraries = new List<Library>(original.Libraries),
        Assets = original.Assets,
        AssetIndex = original.AssetIndex,
        Downloads = original.Downloads,
        Logging = original.Logging,
        JavaVersion = neoforge.JavaVersion
    };

    // 追加NeoForge的依赖库
    if (neoforge.Libraries != null)
    {
        merged.Libraries.AddRange(neoforge.Libraries);
    }

    return merged;
}
```

### 4. 与Fabric现有逻辑的复用点

| 功能 | 复用的Fabric逻辑 |
|------|----------------|
| 下载原版文件 | `DownloadVersionAsync` |
| 获取版本信息 | `GetVersionInfoAsync` |
| 获取版本JSON | `GetVersionInfoJsonAsync` |
| 下载文件 | `DownloadLibraryFileAsync`（改造为通用的文件下载方法） |
| 进度回调 | 复用现有的`Action<double>`进度回调 |
| 日志输出 | 复用现有的日志记录逻辑 |
| 异常处理 | 复用现有的异常处理逻辑 |

### 5. 关键文件和类的设计

#### 5.1 新增类

**NeoForgeInstallerHelper**：
```csharp
public class NeoForgeInstallerHelper
{
    // 解析install_profile.json
    public static dynamic ParseInstallProfile(string installProfilePath);
    
    // 拆包installer.jar
    public static void ExtractInstallerFiles(string installerPath, string extractDirectory);
    
    // 验证MC_SLIM字段
    public static bool VerifyMcSlimField(string mcSlimField, string minecraftVersionId);
    
    // 合并版本JSON
    public static VersionInfo MergeVersionJson(VersionInfo original, VersionInfo neoforge);
    
    // 提取文件从ZIP包
    private static void ExtractFileFromArchive(ZipArchive archive, string entryName, string destinationPath);
}
```

#### 5.2 修改的类

**MinecraftVersionService**：
- 修改`DownloadModLoaderVersionAsync`方法，添加NeoForge分支
- 添加`DownloadNeoForgeVersionAsync`方法
- 添加`DownloadFileAsync`方法（通用文件下载方法）

### 6. 测试计划

1. **单元测试**：
   - 测试版本匹配逻辑
   - 测试JSON合并逻辑
   - 测试拆包逻辑

2. **集成测试**：
   - 测试完整的NeoForge下载流程
   - 测试不同Minecraft版本的适配
   - 测试不同NeoForge版本的适配

3. **异常测试**：
   - 测试网络错误情况
   - 测试文件不存在情况
   - 测试版本不匹配情况

### 7. 预期效果

1. 当用户选择NeoForge作为ModLoader并选择版本后，自动下载NeoForge安装器
2. 支持下载进度显示
3. 自动拆包安装器并提取关键文件
4. 自动合并原版和NeoForge版本JSON
5. 生成完整的NeoForge版本JSON文件
6. 支持异常情况的处理和错误提示
7. 包含详细的日志输出，便于调试和监控

### 8. 后续工作

1. 实现install_profile.json中的processors处理器逻辑
2. 实现BINPATCH补丁应用逻辑
3. 实现NeoForge版本的启动逻辑
4. 添加更多的测试用例
5. 优化UI交互体验

## 三、代码实现计划

1. **第一步**：修改`MinecraftVersionService`类，添加NeoForge分支和`DownloadNeoForgeVersionAsync`方法
2. **第二步**：实现`DownloadFileAsync`通用文件下载方法
3. **第三步**：创建`NeoForgeInstallerHelper`类，实现拆包和JSON合并逻辑
4. **第四步**：测试完整的NeoForge下载流程
5. **第五步**：优化代码，添加日志和异常处理

## 四、关键技术点

1. **文件下载**：使用HttpClient实现异步下载，支持进度显示
2. **ZIP解压缩**：使用System.IO.Compression.ZipFile实现installer.jar的拆包
3. **JSON处理**：使用Newtonsoft.Json实现JSON的解析和合并
4. **进度回调**：使用Action<double>实现下载进度的实时反馈
5. **异常处理**：实现完整的异常处理机制，确保程序稳定性
6. **日志记录**：使用现有的日志记录框架，记录关键操作和错误信息

## 五、风险评估

1. **网络问题**：下载过程中可能遇到网络错误，需要实现重试机制
2. **文件损坏**：下载的installer.jar可能损坏，需要实现校验机制
3. **版本不匹配**：NeoForge版本与Minecraft版本可能不兼容，需要实现验证机制
4. **JSON格式问题**：合并后的JSON格式可能不正确，需要实现验证机制
5. **性能问题**：大文件下载可能影响UI响应，需要实现异步处理

## 六、解决方案

1. **网络问题**：实现最多3次的重试机制，每次重试间隔1秒
2. **文件损坏**：验证下载文件的SHA1哈希或MD5
3. **版本不匹配**：在下载前验证NeoForge版本与Minecraft版本的兼容性
4. **JSON格式问题**：使用JSON Schema验证合并后的JSON格式
5. **性能问题**：使用异步方法和进度回调，确保UI响应流畅

## 七、总结

本方案详细描述了NeoForge适配的第二步实现，包括NeoForge安装器下载、拆包处理和版本JSON合并。方案充分复用了现有的Fabric实现逻辑，同时严格遵循了NeoForge官方适配wiki流程。实现后，用户将能够在XianYu Launcher中下载和安装NeoForge版本，为后续的NeoForge版本启动打下基础。