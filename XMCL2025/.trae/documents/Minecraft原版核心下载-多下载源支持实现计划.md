# Minecraft原版核心下载-多下载源支持实现计划

## 目标
实现Minecraft原版核心下载的多下载源支持，确保所有下载场景（原版下载、ModLoader下载、整合包下载）都能使用配置的下载源。

## 实现步骤

### 1. 修改IDownloadSource接口
- 添加获取客户端JAR下载URL的方法
- 添加获取客户端JSON下载URL的方法
- 确保接口设计统一，支持不同下载源的URL转换

### 2. 更新OfficialDownloadSource实现
- 实现新添加的获取客户端JAR和JSON下载URL方法
- 对于官方源，直接返回原始URL
- 添加必要的debug输出

### 3. 更新BmclapiDownloadSource实现
- 实现新添加的获取客户端JAR和JSON下载URL方法
- 根据BMCLAPI的URL格式转换规则，将原版URL转换为BMCLAPI URL
- 添加必要的debug输出，显示当前下载源和URL转换信息

### 4. 修改MinecraftVersionService中的下载逻辑
- 更新DownloadVersionAsync方法，使用下载源接口获取客户端JAR和JSON的下载URL
- 更新DownloadFabricVersionAsync方法，使用下载源接口获取原版文件的下载URL
- 更新DownloadNeoForgeVersionAsync方法，使用下载源接口获取原版文件的下载URL
- 确保所有涉及原版文件下载的地方都使用下载源接口

### 5. 添加debug输出
- 在关键位置添加debug输出，显示当前使用的下载源
- 显示转换前后的URL，方便调试
- 确保日志清晰，便于开发者追踪下载流程

## 关键技术点

### URL转换规则
- 官方源：直接使用原始URL
- BMCLAPI：将客户端下载URL转换为`https://bmclapi2.bangbang93.com/version/{versions}/client`格式
- 客户端JSON：将client字段替换为json

### 下载源获取方式
- 使用现有的`_downloadSourceFactory.GetSource()`方法获取配置的下载源
- 确保下载源设置与版本列表源设置保持一致

### 统一调用方式
- 所有原版文件下载都通过下载源接口获取URL
- 确保ModLoader下载和整合包下载也使用相同的逻辑
- 保持代码的可扩展性，便于后续添加更多下载源

## 预期效果
- 用户可以在设置页选择下载源
- 选择BMCLAPI时，所有原版文件下载都使用BMCLAPI
- 选择官方源时，使用官方下载链接
- 下载过程中显示详细的debug信息，便于调试
- 支持所有下载场景（原版、ModLoader、整合包）

## 注意事项
- 确保向下兼容，不破坏现有功能
- 保持代码的清晰性和可维护性
- 遵循现有代码的设计模式和命名规范
- 确保所有修改都经过充分测试