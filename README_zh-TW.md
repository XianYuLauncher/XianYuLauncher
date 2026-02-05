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

## ✨ 功能特性

### 🎮 遊戲管理
- **智慧版本管理**：一鍵安裝 Minecraft 原版、Forge、Fabric、Legacy Fabric、NeoForge、Quilt、Optifine、Cleanroom（實驗性）。
- **版本隔離**：每個版本獨立目錄，Mod 互不干擾。
- **世界管理系統**：世界概覽、資料包管理、一鍵啟動存檔。
- **伺服器管理**：新增/管理伺服器、即時狀態檢測（MOTD、線上人數、延遲）、一鍵啟動實例並加入伺服器。

### 🔧 智慧診斷與修復
- **AI 崩潰分析**：支援接入 OpenAI API 相容服務，智慧分析崩潰原因 *（需自行配置 API Endpoint 和 Key）*。
- **XianYu Fixer**：一鍵修復常見問題（Java 版本不匹配、Mod 前置缺失等）。
- **知識庫匹配**：內置錯誤知識庫，快速定位問題並提供解決方案。
- **即時日誌**：遊戲輸出即時監控，支援日誌匯出。

### 📦 資源中心
- **雙平台支援**：Modrinth + CurseForge 資源搜尋下載。
- **智慧依賴管理**：自動識別並下載前置 Mod，避免依賴地獄。
- **收藏夾系統**：收藏喜歡的資源，支援匯入/匯出和批量安裝。
- **中文名稱**：社群資源對於中文使用者支援顯示中文名稱 (資料來源: mcmod)。
- **資源類型**：整合包、Mod、資源包、光影、資料包、地圖。

### ⚡ 效能最佳化
- **分片下載**：多執行緒分片下載，大幅提升下載速度。
- **Java 管理**：自動下載並匹配合適的 Java 版本（8/17/21...）。
- **快取機制**：資源圖示、翻譯結果智慧快取，減少網路請求。

### 🌐 連線功能
- **Terracotta 整合**：P2P 虛擬區域網路，無需公網 IP 即可連線。

### 🎨 使用者體驗
- **Fluent Design**：原生 WinUI 3 介面，現代、高級的視覺效果。
- **3D 外觀預覽**：內置 skinview3d，即時預覽角色外觀。
- **多語言支援**：簡體中文、English。
- **雙通道更新**：Stable/Dev 通道切換，搶先體驗新功能。

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
