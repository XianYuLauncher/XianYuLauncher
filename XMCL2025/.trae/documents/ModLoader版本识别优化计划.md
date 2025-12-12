## 1. 问题分析

* 当前代码大量依赖版本目录名称来识别ModLoader类型（如`versionId.StartsWith("fabric-")`）

* 版本号提取逻辑脆弱，无法处理带-beta等后缀的版本

* 用户自定义版本名称会导致识别失效

* NeoForge 21.11.0-beta被错误识别为21.11.0，导致访问失败

## 2. 优化方案

创建XianYuL.cfg配置文件，在版本下载时生成，包含完整的ModLoader信息，不依赖目录名称。

### 3. 实现步骤

#### 3.1 定义配置文件数据结构

创建`Core/Models/VersionConfig.cs`，包含：

```csharp
public class VersionConfig
{
    public string ModLoaderType { get; set; }  // "fabric", "neoforge", "forge"
    public string ModLoaderVersion { get; set; }  // 完整版本号，如"21.11.0-beta"
    public string MinecraftVersion { get; set; }  // Minecraft版本号
    public DateTime CreatedAt { get; set; }  // 创建时间
}
```

#### 3.2 修改版本下载逻辑

在以下方法中添加配置文件生成：

* `DownloadNeoForgeVersionAsync`

* `DownloadFabricVersionAsync`

* 整合包下载逻辑

**核心逻辑**：

```csharp
// 生成配置文件
var versionConfig = new VersionConfig
{
    ModLoaderType = "neoforge",
    ModLoaderVersion = neoforgeVersion,  // 完整版本号
    MinecraftVersion = minecraftVersionId,
    CreatedAt = DateTime.Now
};
string configPath = Path.Combine(neoforgeVersionDirectory, "XianYuL.cfg");
File.WriteAllText(configPath, JsonConvert.SerializeObject(versionConfig, Formatting.Indented));
```

#### 3.3 更新ModLoader识别逻辑

将所有依赖版本名称识别的代码替换为读取配置文件：

**修改点1**: `MinecraftVersionService.cs`中的`isModLoaderVersion`判断
**修改点2**: 版本列表ViewModel中的版本类型显示
**修改点3**: 启动ViewModel中的Fabric/NeoForge识别
**修改点4**: ModDownloadDetailViewModel中的ModLoader识别

**核心逻辑**：

```csharp
// 读取配置文件识别ModLoader
string configPath = Path.Combine(versionDirectory, "XianYuL.cfg");
if (File.Exists(configPath))
{
    var config = JsonConvert.DeserializeObject<VersionConfig>(File.ReadAllText(configPath));
    modLoaderType = config.ModLoaderType;
    modLoaderVersion = config.ModLoaderVersion;
}
else
{
    // 回退到旧的名称识别逻辑（兼容现有版本）
    if (versionId.StartsWith("fabric-"))
    {
        modLoaderType = "fabric";
        // 从配置文件或其他方式获取版本号
    }
}
```

#### 3.4 修复NeoForge版本处理

* 确保下载时保存完整的版本号到配置文件

* 在`EnsureNeoForgeDependenciesAsync`中使用配置文件获取完整版本号

#### 4. 兼容性处理

* 对现有版本保持向后兼容，优先读取配置文件，失败则回退到名称识别

* 提供工具方法将现有版本转换为新格式

#### 5. 测试要点

* 测试带-beta后缀的NeoForge版本

* 测试自定义版本名称

* 测试Fabric版本

* 测试现有版本的兼容性

* 测试整合包下载

## 4. 预期效果

* 不再依赖版本目录名称识别ModLoader

* 正确处理带后缀的版本号

* 支持用户自定义版本名称

* 统一的版本信息管理

* 更好的可扩展性

