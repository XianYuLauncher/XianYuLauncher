# 依赖库下载功能实现总结

## 已完成的功能

### 1. 依赖库下载功能
- **下载位置**：依赖库将下载至 `.minecraft\libraries` 目录
- **JSON解析**：从版本JSON文件中解析 `libraries` 节点信息
- **智能筛选**：根据操作系统（Windows）自动筛选适用的库
- **原生库支持**：支持下载Windows平台特有的原生库
- **校验机制**：使用SHA1哈希值验证下载文件的完整性
- **增量下载**：已存在且校验通过的库文件将跳过下载

### 2. 游戏启动优化
- **Classpath构建**：自动构建包含所有依赖库的完整Classpath
- **主类获取**：从版本JSON中读取正确的 `MainClass`
- **资源索引**：从版本JSON中读取正确的 `AssetIndex`
- **启动参数**：使用 `-cp` 方式启动游戏，而不是 `-jar` 方式

## 使用方法

### 下载依赖库
1. 打开启动器
2. 进入「下载」页面
3. 选择需要下载的Minecraft版本
4. 点击「下载」按钮
5. 等待下载完成（会显示JAR文件和依赖库的下载进度）

### 启动游戏
1. 进入「启动」页面
2. 选择已下载的Minecraft版本
3. 输入用户名
4. 点击「启动」按钮
5. 观察命令行窗口中的启动信息

## 技术实现细节

### 数据结构扩展
在 `IMinecraftVersionService.cs` 中扩展了版本信息数据结构：
- `VersionInfo` 类添加了 `Libraries`、`MainClass`、`Arguments` 和 `AssetIndex` 属性
- 新增了 `Library`、`LibraryDownloads`、`LibraryArtifact` 等类来表示库信息
- 支持库的规则筛选和原生库处理

### 下载逻辑
在 `MinecraftVersionService.cs` 中实现了 `DownloadLibrariesAsync` 方法：
- 根据操作系统筛选允许的库
- 构建库文件的本地路径（格式：groupId/artifactId/version/filename.jar）
- 下载库文件并验证SHA1哈希值
- 支持原生库的特殊处理

### 启动逻辑
在 `启动ViewModel.cs` 中优化了游戏启动逻辑：
- 从版本JSON中读取完整的版本信息
- 构建包含所有依赖库的Classpath
- 使用正确的MainClass和启动参数
- 显示完整的启动命令供调试使用

## 注意事项

1. **Java版本要求**：确保安装了与Minecraft版本兼容的Java
2. **存储空间**：依赖库可能占用较多存储空间，请确保磁盘有足够空间
3. **网络连接**：首次下载需要良好的网络连接
4. **文件完整性**：如果游戏启动失败，请检查依赖库文件是否完整

## 故障排除

### 常见错误
1. **"找不到或无法加载主类"**：检查MainClass是否正确，依赖库是否完整
2. **ClassNotFoundException**：缺少必要的依赖库，重新下载该版本
3. **Hash校验失败**：网络问题导致下载不完整，重新下载

### 调试方法
- 启动游戏时会显示完整的启动命令
- 可以复制命令到命令提示符中手动执行
- 检查命令行窗口中的详细错误信息