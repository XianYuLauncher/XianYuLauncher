# Implementation Plan: MinecraftVersionService 重构

## Overview

本实施计划将 MinecraftVersionService 的重构工作分解为可执行的任务。采用渐进式重构策略，确保每一步都有测试覆盖，不破坏现有功能。

## Tasks

- [x] 1. Phase 1: 基础设施准备
  - 创建测试项目和基础设施
  - _Requirements: 9.1, 9.6, 15.1_

- [x] 1.1 创建单元测试项目
  - 在解决方案中添加 XianYuLauncher.Tests 项目
  - 添加必要的 NuGet 包：xUnit, Moq, FluentAssertions
  - 配置测试项目引用主项目
  - _Requirements: 9.1, 9.6_

- [-] 1.2 配置 CI/CD 管道（跳过 - 可选任务）
  - 创建 GitHub Actions 或 Azure Pipelines 配置文件
  - 配置自动运行测试
  - 配置代码覆盖率报告生成
  - _Requirements: 15.1, 15.2, 15.3, 15.5_

- [x] 1.3 创建接口定义文件
  - 创建 IDownloadManager 接口
  - 创建 ILibraryManager 接口
  - 创建 IAssetManager 接口
  - 创建 IVersionInfoManager 接口
  - 创建 IModLoaderInstaller 接口
  - 创建 Models/MinecraftModels.cs（包含 VersionInfo、Library 等模型类）
  - 创建 Models/VersionConfig.cs
  - _Requirements: 2.1, 2.2_

- [x] 1.4 创建异常类型层次结构
  - 创建 MinecraftVersionException 基类
  - 创建 DownloadException 类
  - 创建 HashVerificationException 类
  - 创建 LibraryNotFoundException 类
  - 创建 VersionNotFoundException 类
  - 创建 ModLoaderInstallException 类
  - 创建 AssetDownloadException 类
  - 创建 ProcessorExecutionException 类
  - _Requirements: 8.3_

- [-] 2. Phase 2: 下载管理器重构
  - 实现统一的下载管理服务
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 2.1 实现 DownloadManager 类
  - 实现 DownloadFileAsync 方法（单文件下载）
  - 实现 DownloadBytesAsync 方法（下载到内存）
  - 实现 DownloadStringAsync 方法（下载字符串）
  - 实现 DownloadFilesAsync 方法（批量下载）
  - 添加进度报告功能
  - 添加 SHA1 验证功能
  - 添加自动重试机制（指数退避）
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 2.2 为 DownloadManager 编写单元测试
  - 测试单文件下载
  - 测试批量下载
  - 测试进度报告
  - 测试取消下载
  - 测试错误处理和重试
  - **Property 1: 下载文件完整性验证**
  - **Validates: Requirements 3.4**

- [ ]* 2.3 为 DownloadManager 编写单元测试（边界情况）
  - 测试网络错误重试
  - 测试 SHA1 验证失败
  - 测试取消下载
  - 测试并发下载限制
  - _Requirements: 3.3, 3.6, 9.3_

- [x] 2.4 更新 MinecraftVersionService 使用 DownloadManager
  - 在构造函数中注入 IDownloadManager
  - 替换所有直接的 HttpClient 下载调用
  - 保持公共 API 不变
  - _Requirements: 2.4, 13.1_

- [x] 2.5 在 App.xaml.cs 中注册 DownloadManager 服务
  - 添加服务注册代码
  - _Requirements: 2.5_

- [ ] 3. Phase 3: 依赖库管理器重构
  - 实现依赖库下载和管理服务
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

- [x] 3.1 实现 LibraryManager 类
  - 实现 DownloadLibrariesAsync 方法
  - 实现 ExtractNativeLibrariesAsync 方法
  - 实现 IsLibraryDownloaded 方法
  - 实现 GetLibraryPath 方法
  - 添加本地缓存检查逻辑
  - 添加平台过滤逻辑
  - _Requirements: 5.1, 5.2, 5.3, 5.6_

- [x] 3.2 为 LibraryManager 编写单元测试
  - **Property 2: 依赖库路径一致性**
  - **Validates: Requirements 5.2**

- [ ]* 3.3 为 LibraryManager 编写单元测试（原生库）
  - **Property 4: 原生库平台过滤**
  - **Validates: Requirements 5.6**

- [ ]* 3.4 为 LibraryManager 编写单元测试（缓存）
  - **Property 6: 文件缓存命中率**
  - **Validates: Requirements 5.3**

- [x] 3.5 更新 MinecraftVersionService 使用 LibraryManager
  - 在构造函数中注入 ILibraryManager
  - 添加辅助方法委托给 LibraryManager
  - 保持公共 API 不变
  - _Requirements: 2.4, 13.1_

- [x] 3.6 在 App.xaml.cs 中注册 LibraryManager 服务
  - 添加服务注册代码
  - _Requirements: 2.5_

- [ ] 4. Phase 4: 资源管理器重构
  - 实现游戏资源下载和管理服务
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

- [x] 4.1 实现 AssetManager 类
  - 实现 EnsureAssetIndexAsync 方法
  - 实现 DownloadAllAssetObjectsAsync 方法
  - 实现 GetAssetIndexAsync 方法
  - 实现 GetMissingAssetCountAsync 方法
  - 添加资源索引验证逻辑
  - 添加并发下载控制
  - _Requirements: 6.1, 6.2, 6.4_

- [x] 4.2 为 AssetManager 编写单元测试
  - 测试资源索引下载和验证
  - 测试资源对象批量下载
  - 测试并发下载控制
  - _Requirements: 6.2, 6.4, 9.2_

- [ ]* 4.3 为 AssetManager 编写单元测试（进度报告）
  - **Property 8: 进度报告单调性**
  - **Validates: Requirements 6.6**

- [x] 4.4 更新 MinecraftVersionService 使用 AssetManager
  - 在构造函数中注入 IAssetManager
  - 添加辅助方法委托给 AssetManager
  - 保持公共 API 不变
  - _Requirements: 2.4, 13.1_

- [x] 4.5 在 App.xaml.cs 中注册 AssetManager 服务
  - 添加服务注册代码
  - _Requirements: 2.5_

- [ ] 5. Phase 5: 版本信息管理器重构
  - 实现版本信息获取和缓存服务
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

- [x] 5.1 实现 VersionInfoManager 类
  - 实现 GetVersionManifestAsync 方法
  - 实现 GetVersionInfoAsync 方法
  - 实现 GetVersionInfoJsonAsync 方法
  - 实现 GetInstalledVersionsAsync 方法
  - 实现 GetVersionConfigAsync 方法
  - 实现 SaveVersionConfigAsync 方法
  - 实现 MergeVersionInfo 方法
  - 添加本地缓存优先逻辑
  - 添加版本继承处理逻辑
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 5.2 为 VersionInfoManager 编写单元测试
  - **Property 3: 版本信息继承正确性**
  - **Validates: Requirements 7.4**

- [ ]* 5.3 为 VersionInfoManager 编写单元测试（配置文件）
  - **Property 9: 版本配置文件一致性**
  - **Validates: Requirements 7.5**

- [ ]* 5.4 为 VersionInfoManager 编写单元测试（缓存）
  - 测试本地缓存优先逻辑
  - 测试网络回退逻辑
  - 测试版本清单缓存
  - _Requirements: 7.2, 7.3, 9.2_

- [x] 5.5 更新 MinecraftVersionService 使用 VersionInfoManager
  - 在构造函数中注入 IVersionInfoManager
  - 添加辅助方法委托给 VersionInfoManager
  - 保持公共 API 不变
  - _Requirements: 2.4, 13.1_

- [x] 5.6 在 App.xaml.cs 中注册 VersionInfoManager 服务
  - 添加服务注册代码
  - _Requirements: 2.5_

- [x] 6. Phase 6: ModLoader 安装器重构
  - 为每种 ModLoader 创建独立的安装器
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [x] 6.1 创建 ModLoader 安装器基类
  - 定义共同的安装流程模板
  - 提取可复用的辅助方法
  - 实现通用的错误处理逻辑
  - _Requirements: 4.2, 4.3_

- [x] 6.2 实现 FabricInstaller 类
  - 实现 InstallAsync 方法
  - 实现 GetAvailableVersionsAsync 方法
  - 复用基类的共同逻辑
  - _Requirements: 4.1, 4.4_

- [x] 6.3 为 FabricInstaller 编写单元测试
  - 测试正常安装流程
  - 测试错误处理
  - 测试版本列表获取
  - _Requirements: 9.2, 9.3_

- [x] 6.4 实现 ForgeInstaller 类
  - 实现 InstallAsync 方法
  - 实现 GetAvailableVersionsAsync 方法
  - 处理 Forge 特有的处理器逻辑
  - _Requirements: 4.1, 4.4_

- [x] 6.5 为 ForgeInstaller 编写单元测试
  - 测试正常安装流程
  - 测试处理器执行
  - 测试错误处理
  - _Requirements: 9.2, 9.3_

- [x] 6.6 实现 NeoForgeInstaller 类
  - 实现 InstallAsync 方法
  - 实现 GetAvailableVersionsAsync 方法
  - 处理 NeoForge 特有的处理器逻辑
  - _Requirements: 4.1, 4.4_

- [x] 6.7 为 NeoForgeInstaller 编写单元测试
  - 测试正常安装流程
  - 测试处理器执行
  - 测试错误处理
  - _Requirements: 9.2, 9.3_

- [x] 6.8 实现 OptifineInstaller 类
  - 实现 InstallAsync 方法
  - 实现 GetAvailableVersionsAsync 方法
  - 处理 Optifine 特有的安装逻辑
  - _Requirements: 4.1, 4.4_

- [x] 6.9 为 OptifineInstaller 编写单元测试
  - 测试正常安装流程
  - 测试错误处理
  - _Requirements: 9.2, 9.3_

- [x] 6.10 实现 QuiltInstaller 类
  - 实现 InstallAsync 方法
  - 实现 GetAvailableVersionsAsync 方法
  - 复用基类的共同逻辑
  - _Requirements: 4.1, 4.4_

- [x] 6.11 为 QuiltInstaller 编写单元测试
  - 测试正常安装流程
  - 测试错误处理
  - _Requirements: 9.2, 9.3_

- [x] 6.12 更新 MinecraftVersionService 使用新的安装器
  - 在构造函数中注入所有 IModLoaderInstaller 实现
  - 使用策略模式选择合适的安装器
  - 保持公共 API 不变
  - _Requirements: 2.4, 4.5, 13.1_

- [x] 6.13 在 App.xaml.cs 中注册所有 ModLoader 安装器
  - 添加服务注册代码
  - _Requirements: 2.5_

- [ ] 7. Phase 7: 集成测试和性能优化
  - 编写端到端集成测试并优化性能
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 11.1, 11.2, 11.3, 11.4, 11.5_

- [ ] 7.1 创建集成测试项目
  - 创建 XianYuLauncher.IntegrationTests 项目
  - 配置测试数据和模拟环境
  - _Requirements: 10.1_

- [ ]* 7.2 编写版本下载集成测试
  - 测试完整的原版 Minecraft 下载流程
  - 测试依赖库下载
  - 测试资源文件下载
  - _Requirements: 10.2, 10.3, 10.4_

- [ ]* 7.3 编写 ModLoader 安装集成测试
  - 测试 Fabric 完整安装流程
  - 测试 Forge 完整安装流程
  - 测试 NeoForge 完整安装流程
  - _Requirements: 10.3_

- [ ]* 7.4 编写性能测试
  - 测试下载速度
  - 测试并发下载效率
  - 测试内存使用
  - 测试 CPU 使用
  - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [ ] 7.5 性能优化
  - 优化并发下载策略
  - 优化文件 I/O 操作
  - 优化内存使用
  - 添加性能监控日志
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

- [ ] 8. Phase 8: 文档和清理
  - 更新文档并清理代码
  - _Requirements: 12.3, 12.4, 12.5, 12.6, 14.1, 14.2, 14.3, 14.4, 14.5, 14.6_

- [ ] 8.1 添加 XML 文档注释
  - 为所有公共接口添加注释
  - 为所有公共类添加注释
  - 为所有公共方法添加注释
  - _Requirements: 12.3_

- [ ] 8.2 创建架构文档
  - 创建 UML 类图
  - 创建序列图
  - 更新架构设计文档
  - _Requirements: 14.1, 14.2, 14.3_

- [ ] 8.3 创建 API 文档
  - 使用 DocFX 或类似工具生成 API 文档
  - 添加使用示例
  - _Requirements: 14.4_

- [ ] 8.4 创建迁移指南
  - 编写从旧 API 到新 API 的迁移指南
  - 提供代码示例
  - 说明破坏性更改（如果有）
  - _Requirements: 13.4, 14.5_

- [ ] 8.5 更新 README 文件
  - 更新项目描述
  - 更新架构说明
  - 添加重构相关的说明
  - _Requirements: 14.6_

- [ ] 8.6 代码清理
  - 移除未使用的代码
  - 移除代码重复
  - 统一代码风格
  - _Requirements: 12.4, 12.5_

- [ ] 8.7 最终代码审查
  - 进行完整的代码审查
  - 确保所有测试通过
  - 确保代码覆盖率达标
  - _Requirements: 9.5, 12.1, 12.2_

- [ ] 9. Checkpoint - 确保所有测试通过
  - 确保所有测试通过，询问用户是否有问题

- [ ]* 10. 向后兼容性验证
  - **Property 10: API 向后兼容性**
  - **Validates: Requirements 13.2**

## Notes

- 标记为 `*` 的任务是可选的测试任务，可以根据项目进度决定是否执行
- 每个 Phase 完成后应该运行所有测试，确保没有破坏现有功能
- 建议使用 Git 分支来管理每个 Phase 的开发，便于回滚和代码审查
- 性能测试应该在真实环境中运行，以获得准确的性能数据
- 集成测试可以使用 Docker 来模拟网络环境，提高测试的可靠性
