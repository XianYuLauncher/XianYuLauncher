![XianYu Launcher Cover](/XianYuLauncher/Assets/cover.png)

<div align="center">

# XianYu Launcher

一個設計現代化、功能豐富的 Minecraft Java 版啟動器，採用原生 Windows Fluent Design 設計風格。

> **免責聲明**：本專案為非官方專案，與 Mojang Studios 或 Microsoft 無任何關聯。

[![GitHub Stars](https://img.shields.io/github/stars/XianYuLauncher/XianYuLauncher.svg?style=flat-square&label=⭐%20Stars)](https://github.com/XianYuLauncher/XianYuLauncher)
[![GitHub Release](https://img.shields.io/github/v/release/XianYuLauncher/XianYuLauncher?style=flat-square%20Release&logo=github)](https://github.com/XianYuLauncher/XianYuLauncher/releases)
[![Docs Online](https://img.shields.io/badge/Docs-文檔-0EA5E9?style=flat-square&logo=gitbook&logoColor=white)](https://docs.xianyulauncher.com)
[![Bilibili](https://img.shields.io/badge/bilibili-@Spirit靈動工作室-FF69B4?style=flat-square&logo=bilibili&logoColor=white)](https://space.bilibili.com/3493299136498148)

[English](README.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md)
</div>

## 功能特性

- **流暢、現代的 UI**：基於 WinUI 3 構建，提供原生且流暢的 Windows 體驗。
- **內置連線支援**：整合由 **Terracotta** 驅動的 P2P 網路大廳，讓您像在區域網路 (虛擬區域網路) 一樣輕鬆與朋友連線遊玩。
- **AI 驅動的崩潰分析**：提供連接到相容 OpenAI API 規範的 LLM 服務以分析遊戲崩潰的能力。*注意：不包含內置 AI 模型；使用者必須配置自己的 API Endpoint 和 Key。*
- **綜合資源中心**：搜索並一鍵安裝來自 Modrinth 和 CurseForge 的 整合包、模組、資源包 和 光影包。
- **智慧環境管理**：自動下載並匹配合適的 Java 運行時 (JRE/JDK)。
- **外觀管理**：內置 3D 外觀檢視器和外觀管理器。
- **載入器支援**：一鍵安裝 Forge、Fabric、NeoForge、Quilt、Optifine 和 Cleanroom (實驗性)。
- **版本管理**：便捷安裝，透過獨立目錄讓您的版本和 Mods 井井有條。
- **即時日誌**：透過即時日誌檢視監控遊戲輸出。
- **多語言支援**：支援英語和中文。

## 快速開始

### 環境要求
- Windows 10 1809 (17763) 或更高版本
- .NET 10.0 SDK

### 安裝

[![從 Microsoft Store 取得](https://get.microsoft.com/images/zh-tw%20dark.svg)](https://apps.microsoft.com/detail/9pcnpgl7j6ks?mode=direct)

**或手動安裝：**

1. **下載**：從 [Releases](https://github.com/XianYuLauncher/XianYuLauncher/releases) 頁面獲取最新版本。
2. **解壓**：將下載的壓縮包解壓到您喜歡的位置。
3. **安裝與執行**：參考包中包含的 `安装教程.txt` 檔案，按照步驟完成安裝。

## 技術棧

- **框架**：.NET 10.0
- **UI**：WinUI 3
- **架構**：MVVM (CommunityToolkit.Mvvm)
- **Windows App SDK**：1.8.251106002

## 開源協議

本專案作為開源軟體在 **MIT License** 下發布。

關於第三方庫和資料來源的說明，請參閱 [NOTICE.md](NOTICE.md)。

### 開源聲明
- 本專案在 MIT License 下開源。
- 所有開原始碼受 MIT License 保護。

### MIT License

完整的協議文字請參閱 [LICENSE](LICENSE) 檔案。

## 聯絡方式

- **GitHub**: [XianYuLauncher](https://github.com/XianYuLauncher/XianYuLauncher)
- **Issues**: [報告 Bug 或請求功能](https://github.com/XianYuLauncher/XianYuLauncher/issues)

## 程式碼簽章策略

免費程式碼簽章由 [SignPath.io](https://about.signpath.io/) 提供，憑證由 [SignPath Foundation](https://signpath.org/) 提供。

本專案使用 SignPath 以確保發布的完整性和真實性。

> **注意**：由於開發者額度不足，暫時可能不會使用 SignPath 簽章。

- **流程**：所有獨立發行版（不包括 Microsoft Store 構建）均使用 GitHub Actions 構建，並由 SignPath 自動簽章。
- **隱私與條款**：
  - [使用條款](https://docs.qq.com/doc/DVnZxWHNMUEtxRGVV)
  - [隱私政策](https://docs.qq.com/doc/DVnFIaUVhb2NXRXRz)

---

**XianYu Launcher** - 提升您的 Minecraft 體驗！
