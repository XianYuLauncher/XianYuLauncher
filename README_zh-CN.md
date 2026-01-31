![XianYu Launcher Cover](/XianYuLauncher/Assets/cover.png)

<div align="center">

# XianYu Launcher

一个设计现代化、功能强大的 Minecraft Java 版启动器，采用原生 Windows Fluent Design 设计风格。

> **免责声明**：本项目为非官方项目，与 Mojang Studios 或 Microsoft 无任何关联。

[![GitHub Stars](https://img.shields.io/github/stars/XianYuLauncher/XianYuLauncher.svg?style=flat-square&label=⭐%20Stars)](https://github.com/XianYuLauncher/XianYuLauncher)
[![GitHub Release](https://img.shields.io/github/v/release/XianYuLauncher/XianYuLauncher?style=flat-square%20Release&logo=github)](https://github.com/XianYuLauncher/XianYuLauncher/releases)
[![Docs Online](https://img.shields.io/badge/Docs-文档-0EA5E9?style=flat-square&logo=gitbook&logoColor=white)](https://docs.xianyulauncher.com)
[![Bilibili](https://img.shields.io/badge/bilibili-@Spirit灵动工作室-FF69B4?style=flat-square&logo=bilibili&logoColor=white)](https://space.bilibili.com/3493299136498148)

[English](README.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md)
</div>

## 功能特性

- **流畅、现代的 UI**: 基于 WinUI 3 构建，提供原生且流畅的 Windows 体验。
- **内置联机支持**: 集成由 **Terracotta** 驱动的 P2P 网络大厅，让您像在局域网 (虚拟局域网) 一样轻松与朋友联机游玩。
- **AI 驱动的崩溃分析**: 提供连接到兼容 OpenAI API 规范的 LLM 服务以分析游戏崩溃的能力。*注意：不包含内置 AI 模型；用户必须配置自己的 API Endpoint 和 Key。*
- **综合资源中心**：搜索并一键安装来自 Modrinth 和 CurseForge 的 整合包、模组、资源包 和 光影包。
- **智能环境管理**：自动下载并匹配合适的 Java 运行时 (JRE/JDK)。
- **皮肤管理**：内置 3D 皮肤查看器和皮肤管理器。
- **加载器支持**：一键安装 Forge、Fabric、NeoForge、Quilt、Optifine 和 Cleanroom (实验性)。
- **版本管理**：便捷安装，通过独立目录让您的版本和 Mods 井井有条。
- **实时日志**：通过实时日志查看监控游戏输出。
- **多语言支持**：支持英语和中文。

## 快速开始

### 环境要求
- Windows 10 1809 (17763) 或更高版本
- .NET 10.0 SDK

### 安装

[![从 Microsoft Store 获取](https://get.microsoft.com/images/zh-cn%20dark.svg)](https://apps.microsoft.com/detail/9pcnpgl7j6ks?mode=direct)

**或手动安装：**

1. **下载**：从 [Releases](https://github.com/XianYuLauncher/XianYuLauncher/releases) 页面获取最新版本。
2. **解压**：将下载的压缩包解压到您喜欢的位置。
3. **安装与运行**：参考包中包含的 `安装教程.txt` 文件，按照步骤完成安装。
“`

## 技术栈

- **框架**：.NET 10.0
- **UI**：WinUI 3
- **架构**：MVVM (CommunityToolkit.Mvvm)
- **Windows App SDK**：1.8.251106002

## 开源协议

本项目作为开源软件在 **MIT License** 下发布。

关于第三方库和数据来源的说明，请参阅 [NOTICE.md](NOTICE.md)。

### 开源声明
- 本项目在 MIT License 下开源。
- 所有开源代码受 MIT License 保护。

### MIT License

完整的协议文本请参阅 [LICENSE](LICENSE) 文件。

## 联系方式

- **GitHub**: [XianYuLauncher](https://github.com/XianYuLauncher/XianYuLauncher)
- **Issues**: [报告 Bug 或请求功能](https://github.com/XianYuLauncher/XianYuLauncher/issues)

## 代码签名策略

免费代码签名由 [SignPath.io](https://about.signpath.io/) 提供，证书由 [SignPath Foundation](https://signpath.org/) 提供。

本项目使用 SignPath 以确保发布的完整性和真实性。

> **注意**：由于开发者额度不足，暂时可能不会使用SignPath签名。

- **流程**：所有独立发行版（不包括 Microsoft Store 构建）均使用 GitHub Actions 构建，并由 SignPath 自动签名。
- **隐私与条款**：
  - [使用条款](https://docs.qq.com/doc/DVnZxWHNMUEtxRGVV)
  - [隐私政策](https://docs.qq.com/doc/DVnFIaUVhb2NXRXRz)

---

**XianYu Launcher** - 提升您的 Minecraft 体验！
