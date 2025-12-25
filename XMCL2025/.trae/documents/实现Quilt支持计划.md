# 实现Quilt支持计划

## 1. 扩展下载源接口

### 1.1 修改`IDownloadSource.cs`
- 添加Quilt相关方法：
  - `GetQuiltVersionsUrl(string minecraftVersion)`
  - `GetQuiltProfileUrl(string minecraftVersion, string quiltVersion)`

## 2. 实现下载源支持

### 2.1 修改`OfficialDownloadSource.cs`
- 实现Quilt版本列表URL获取
- 实现Quilt完整配置URL获取
- 输出调试日志

### 2.2 修改`BmclapiDownloadSource.cs`
- 实现Quilt版本列表URL获取
- 实现Quilt完整配置URL获取
- 输出调试日志

## 3. 创建Quilt服务

### 3.1 创建`QuiltService.cs`
- 参考`FabricService.cs`实现
- 支持从双下载源获取Quilt版本列表
- 实现BMCLAPI 404时切换到官方源的逻辑
- 输出详细调试信息

## 4. 创建Quilt模型类

### 4.1 创建`QuiltLoaderVersion.cs`
- 参考`FabricLoaderVersion.cs`
- 适配Quilt的JSON结构

## 5. 实现Quilt下载逻辑

### 5.1 创建`MinecraftVersionService.Quilt.cs`
- 实现`DownloadQuiltVersionAsync`方法
- 参考Fabric的下载流程
- 处理Quilt的JSON结构
- 支持不同Maven坐标来源的下载源切换
- 实现依赖库下载
- 实现版本JSON合并
- 输出详细调试信息

## 6. 扩展ModLoader选择逻辑

### 6.1 修改`ModLoader选择ViewModel.cs`
- 添加Quilt作为可选ModLoader
- 实现Quilt版本加载逻辑
- 实现Quilt下载命令

## 7. 实现失败切换逻辑

- 在Quilt版本获取和下载过程中添加失败切换逻辑
- 确保BMCLAPI 404时能切换到官方源
- 输出详细的错误日志

## 8. 调试信息支持

- 在所有关键步骤添加调试日志
- 显示下载源和请求URL
- 显示下载进度和结果

## 9. 测试和验证

- 确保Quilt版本列表能正确获取
- 确保Quilt能成功下载和安装
- 确保双下载源切换正常工作
- 确保调试信息正确输出

## 10. 代码结构优化

- 确保代码符合现有项目结构
- 使用partial class保持代码组织
- 复用现有逻辑，避免重复代码

这个计划将确保Quilt支持的实现与现有的Fabric支持保持一致，同时满足用户的所有要求。