![XianYu Launcher Cover](/XianYuLauncher/Assets/ReadmeHero_zh.png)

<div align="center">

# XianYu Launcher

一个设计现代化、功能强大的 Minecraft Java 版启动器，采用原生 Windows Fluent Design 设计风格。

> **免责声明**：本项目为非官方项目，与 Mojang Studios 或 Microsoft 无任何关联。

[![GitHub Stars](https://img.shields.io/github/stars/XianYuLauncher/XianYuLauncher.svg?style=flat-square&label=⭐%20Stars)](https://github.com/XianYuLauncher/XianYuLauncher)
[![GitHub Release](https://img.shields.io/github/v/release/XianYuLauncher/XianYuLauncher?style=flat-square%20Release&logo=github)](https://github.com/XianYuLauncher/XianYuLauncher/releases)
[![Docs Online](https://img.shields.io/badge/Docs-文档-0EA5E9?style=flat-square&logo=gitbook&logoColor=white)](https://docs.xianyulauncher.com)
[![Bilibili](https://img.shields.io/badge/bilibili-@SpiritStudio-FF69B4?style=flat-square&logo=bilibili&logoColor=white)](https://space.bilibili.com/3493299136498148)

[English](README.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md)
</div>

## ✨ 功能特性

### 🎮 游戏管理
- **智能版本管理**：一键安装 Minecraft 原版、Forge、Fabric、Legacy Fabric、NeoForge、Quilt、Optifine、Cleanroom、LiteLoader
- **版本隔离**：每个版本独立目录，Mod 互不干扰
- **世界管理系统**：世界概览、数据包管理、一键启动存档
- **服务器管理**：添加/管理服务器、实时状态检测（MOTD、在线人数、延迟）、一键启动实例并加入服务器

### 🔧 智能诊断与修复
- **AI 崩溃分析**：支持接入 OpenAI API 兼容服务，智能分析崩溃原因 *（需自行配置 API Endpoint 和 Key）*
- **XianYu Fixer**：一键修复常见问题（Java 版本不匹配、Mod 前置缺失等）
- **知识库匹配**：内置错误知识库，快速定位问题并提供解决方案
- **实时日志**：游戏输出实时监控，支持日志导出

### 📦 资源中心
- **双平台支持**：Modrinth + CurseForge 资源搜索下载
- **智能依赖管理**：自动识别并下载前置 Mod，避免依赖地狱
- **收藏夹系统**：收藏喜欢的资源，支持导入/导出和批量安装
- **中文名称**：社区资源对于中文用户支持显示中文名称 (数据来源:mcmod)
- **资源类型**：整合包、Mod、资源包、光影、数据包、地图

### ⚡ 性能优化
- **分片下载**：多线程分片下载，大幅提升下载速度
- **Java 管理**：自动下载并匹配合适的 Java 版本（8/17/21...）
- **缓存机制**：资源图标、翻译结果智能缓存，减少网络请求

### 🌐 联机功能
- **Terracotta 集成**：P2P 虚拟局域网，无需公网 IP 即可联机

### 🎨 用户体验
- **Fluent Design**：原生 WinUI 3 界面，现代、高级的视觉效果
- **3D 皮肤预览**：内置 skinview3d，实时预览角色皮肤
- **多语言支持**：简体中文、English
- **双通道更新**：Stable/Dev 通道切换，抢先体验新功能

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

- **隐私与条款**：
  - [使用条款](https://docs.qq.com/doc/DVnZxWHNMUEtxRGVV)
  - [隐私政策](https://docs.qq.com/doc/DVnFIaUVhb2NXRXRz)

---

**XianYu Launcher** - 提升您的 Minecraft 体验！
