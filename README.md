# XianYuLauncher

一个基于 .NET 10.0 和 WinUI 3 开发的现代化 Minecraft 启动器，提供流畅的用户体验和丰富的功能。

## 项目简介

XianYuLauncher 是一个功能完整的 Minecraft 启动器，支持多种 Minecraft 版本管理、账号登录、游戏设置和实时日志查看等功能。采用现代化的 UI 设计和流畅的交互体验，为玩家提供便捷的游戏管理工具。

> **本项目现已完全开源！** 采用 MIT 协议，欢迎社区贡献和使用。

## 主要功能

### 核心功能
- 支持多种 Minecraft 版本的安装和管理
- 账号登录系统（支持微软、离线、外置登录）
- 游戏设置自定义（内存分配、分辨率等）
- 实时游戏日志查看功能
- 游戏崩溃分析和日志导出
- 支持 Mod 管理
- 支持资源包和光影包管理
- 多语言支持

### 特色功能
- 现代化的 WinUI 3 界面设计
- 流畅的性能和响应速度
- 多角色管理
- 详细的版本统计信息

## 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 10.0 | 核心框架 |
| WinUI 3 | 1.8 | UI 框架 |
| C# | 13.0 | 开发语言 |
| Microsoft.WindowsAppSDK | 1.8.251106002 | Windows 应用 SDK |

## 开源协议

本项目采用 **MIT 协议**完全开源。

### MIT 许可证

本项目使用 MIT 许可证，这意味着：
- 可以自由使用、复制、修改、合并、发布、分发、再许可和/或销售本软件
- 可以用于商业用途
- 可以修改源代码
- 可以私有使用
- 需要在副本中包含版权声明和许可声明
- 软件按"原样"提供，不提供任何形式的保证

完整的许可证文本请查看 [LICENSE](LICENSE) 文件。

## 快速开始

### 环境要求
- Windows 10 1809 (17763) 或更高版本

### 运行已发布版本

1. 前往 [Releases](../../releases) 页面下载最新版本
2. 解压到任意文件夹，按照'安装说明.txt'中的步骤进行安装
3. 安装完毕后，开始使用 XianYuLauncher

### 从源码构建

#### 环境要求
- Visual Studio 2022 (17.12 或更高版本)
- .NET 10.0 SDK
- Windows 10 SDK (10.0.19041.0 或更高版本)

#### 构建步骤

1. 克隆仓库
   ```bash
   git clone https://github.com/XianYuLauncher/XianYuLauncher.git
   cd XianYuLauncher
   ```

2. 配置 secrets.json
   ```bash
   # 复制示例配置文件
   copy XianYuLauncher\secrets.example.json XianYuLauncher\secrets.json
   # 编辑 secrets.json，填入你的配置（可选）
   ```

3. 使用 Visual Studio 打开 `XianYuLauncher.slnx`

4. 还原 NuGet 包
   ```bash
   dotnet restore
   ```

5. 构建项目
   - 在 Visual Studio 中按 `Ctrl+Shift+B`
   - 或使用命令行：
     ```bash
     dotnet build
     ```

6. 运行项目
   - 在 Visual Studio 中按 `F5`
   - 或使用命令行：
     ```bash
     dotnet run --project XianYuLauncher
     ```

## 贡献指南

欢迎所有形式的贡献！无论是报告 Bug、提出新功能建议，还是提交代码改进。

### 如何贡献

1. Fork 本仓库
2. 创建你的特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交你的改动 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启一个 Pull Request

### 代码规范

- 遵循 C# 编码规范
- 保持代码整洁和可读性
- 为新功能添加适当的注释
- 确保代码通过所有测试

## 问题反馈

如果你发现了 Bug 或有功能建议，请在 [Issues](../../issues) 页面提交。

提交 Issue 时请包含：
- 问题的详细描述
- 复现步骤
- 预期行为和实际行为
- 系统环境信息（Windows 版本、.NET 版本等）
- 相关截图或日志（如果有）

## 致谢

感谢所有为本项目做出贡献的开发者和使用者！

完整的第三方库声明请查看 [NOTICE.md](NOTICE.md) 文件。

特别感谢：
- [bangbang93](https://github.com/bangbang93) - BMCLAPI 镜像源支持
- 所有提交 Issue 和 PR 的贡献者
- 使用并支持本项目的玩家们

## 联系方式

- 项目主页：[GitHub](https://github.com/XianYuLauncher/XianYuLauncher)
- 问题反馈：[Issues](../../issues)
- 讨论交流：[Discussions](../../discussions)

## Star History

如果这个项目对你有帮助，请给我们一个 Star

---

**XianYuLauncher** - 让 Minecraft 游戏体验更加完美
