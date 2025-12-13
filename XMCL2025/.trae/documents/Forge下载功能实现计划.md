# Forge下载功能实现计划

## 实现目标

支持双下载源（官方源和BMCLAPI）的Forge下载功能，实现以下流程：
1. 创建版本目录
2. 下载原版MC核心文件
3. 下载原版MC JSON文件
4. 下载Forge installer.jar包
5. 自动解压至临时目录

## 技术要点

1. **使用partial class**：在`MinecraftVersionService.Forge.cs`中实现，不修改主文件
2. **下载源工厂**：使用现有的下载源工厂接口获取Forge安装包URL
3. **双下载源支持**：官方源和BMCLAPI源
4. **流程清晰**：按照指定步骤实现下载流程

## 实现步骤

### 1. 实现DownloadForgeVersionAsync方法
- 在`MinecraftVersionService.Forge.cs`中添加`DownloadForgeVersionAsync`方法
- 接收参数：Minecraft版本ID、Forge版本、版本目录、库目录、进度回调、取消令牌、自定义版本名称
- 返回类型：Task

### 2. 创建版本目录
- 使用`Directory.CreateDirectory`创建版本目录
- 确保目录结构正确

### 3. 下载原版MC核心文件和JSON文件
- 复用现有逻辑，从下载源获取原版Minecraft版本信息
- 下载原版JAR文件到Forge版本目录
- 下载原版JSON文件到Forge版本目录

### 4. 获取Forge安装包URL
- 使用下载源工厂获取当前配置的下载源
- 调用下载源的`GetForgeInstallerUrl`方法获取安装包URL
- 支持官方源和BMCLAPI源

### 5. 下载Forge installer.jar包
- 使用HttpClient下载Forge安装包
- 保存到临时目录
- 添加进度报告

### 6. 自动解压至临时目录
- 使用ZipArchive解压Forge安装包
- 保存到临时目录
- 添加进度报告

### 7. 生成版本配置文件
- 创建VersionConfig对象，记录ModLoader类型和版本
- 保存到版本目录的XianYuL.cfg文件

## 代码结构

```csharp
public partial class MinecraftVersionService
{
    private async Task DownloadForgeVersionAsync(
        string minecraftVersionId,
        string forgeVersion,
        string versionsDirectory,
        string librariesDirectory,
        Action<double> progressCallback,
        CancellationToken cancellationToken = default,
        string customVersionName = null)
    {
        // 实现下载流程
    }
}
```

## 依赖关系

- 现有下载源工厂：`DownloadSourceFactory`
- 现有下载源接口：`IDownloadSource`
- 现有配置服务：`ILocalSettingsService`
- 现有文件服务：`IFileService`

## 测试要点

1. 确保支持官方源下载
2. 确保支持BMCLAPI源下载
3. 确保下载流程正确执行
4. 确保进度报告正常
5. 确保临时目录创建和文件解压正常
6. 确保版本配置文件生成正确

## 文件修改清单

- `XMCL2025/Core/Services/MinecraftVersionService.Forge.cs` - 实现Forge下载功能
- 不修改任何现有代码，只在新文件中添加新功能

## 预期效果

- 成功下载Forge版本
- 支持双下载源
- 下载流程完整，包括创建目录、下载文件、解压等
- 进度报告准确
- 版本配置文件生成正确