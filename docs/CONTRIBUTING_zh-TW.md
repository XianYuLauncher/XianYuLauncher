![XianYu Launcher 貢獻指南](/XianYuLauncher/Assets/ContributingHero_zh-TW.png)

[English](../CONTRIBUTING.md) | [簡體中文](CONTRIBUTING_zh-CN.md) | [繁體中文](CONTRIBUTING_zh-TW.md)

# 參與貢獻

感謝你對 XianYu Launcher 的關注。本文說明如何提交 Issue 與參與程式碼貢獻；前半部為 Issue 填寫說明，後半部為 PR 與本機建置相關約定。

---

## 如何提交 Issue

### 從哪開始

1. 開啟本儲存庫 GitHub 頁面的 **Issues** → **New issue**。
2. **請優先選用範本**（比空白 Issue 更容易被處理）：
   - **漏洞回報**：當機、錯誤行為、與預期不符的缺陷。
   - **功能建議**：新能力、體驗最佳化、產品向想法。

在網頁上選擇對應類型即可，系統會帶好標題前綴與標籤提示。

### 回報 Bug 時多寫一點什麼

- **啟動器版本**、**Windows 版本**、相關 **ModLoader 類型/版本**（若與遊戲異常有關）。
- **重現步驟**：依 1、2、3 寫清楚「點了什麼、先後順序」，能穩定重現最好。
- **日誌**：範本中已說明從 **設定 → 關於 → 快速動作 → 開啟日誌目錄** 取得日誌；將**相關片段**貼進 Issue（以程式碼區塊包起來）或上傳檔案，比只有一句「當機了」有用得多。
- **螢幕擷圖**：有對話方塊、介面異常時盡量附圖。

若暫時不確定算 Bug 還是建議，任選較接近的一類即可，開發者會再歸類。

### 提出功能建議時多寫一點什麼

- **解決什麼痛點**：現在哪裡不順、多花了什麼時間。
- **你期望的行為或介面**：盡量具體，若有參考（其他軟體、草圖）可放在「補充材料」裡。

### 其他習慣

- 多個無關問題請拆成多個 Issue，方便追蹤與關閉。
- 避免重複開啟已討論過的相同問題，可以先看一遍 Issue 列表。
- 語言均可；關鍵資訊寫清楚比措辭完美更重要。

---

## 程式碼貢獻（PR）

準備直接改儲存庫時，**架構限制、下載/對話方塊等規範**請以 **`.github/copilot-instructions.md`** 為準（與維護者、Copilot 共用的說明）。

### 提交說明

採用 **Conventional Commits**：

- **`type` 使用英文**：如 `feat`、`fix`、`refactor`、`chore` 等。
- **`scope` 可選**，如 `(protocol)`、`(ModDownloadDetailViewModel)`。
- **標題與本文均可**，依慣用語言自行選擇即可，簡潔說明**做了什麼、為什麼**；較大變更請在本文中補充背景與遷移注意點。

範例：

```text
fix(setting): 修復設定項未生效

- 將錯誤的屬性讀取鍵改為正確鍵
- 同步各呼叫端
```

### 提交流程建議

1. 從 `main` 新建功能分支。
2. 保持變更 **聚焦**：一個 PR 盡量解決一類問題，避免無關格式化或大範圍重排。
3. 修復 Bug 時，若條件允許，請在 PR 描述中關聯 Issue，並寫清 **驗證方式**。
4. 提交 PR 後留意 Review；有較大設計分歧時，**先開 Issue 對齊**再大規模改程式會比較順。

---

## 環境與建置

若本機已具備 WinUI / 桌面開發環境，可略過本節；需要與儲存庫說明對齊時可參考。

- **系統**：Windows 10 1809 (17763) 或更新版本（與 README 一致）。
- **工具**：**.NET 10.0**；開發 WinUI 建議使用含 **.NET 桌面開發**、**WinUI 應用程式開發**、**MSBuild** 等工作負載的 **Visual Studio 2026 或以上版本**（以能正常開啟本方案為準）。

**常用命令**（儲存庫根目錄）：

```bash
# 主應用程式（WinUI）
msbuild XianYuLauncher/XianYuLauncher.csproj -p:Configuration=Debug -p:Platform=x64 -nologo -v:minimal

# Core
dotnet build XianYuLauncher.Core/XianYuLauncher.Core.csproj

# 測試（將 <測試專案.csproj> 換成實際要測的測試專案）
dotnet test <測試專案.csproj>
```

---

## 其他入口

- **文件網站**、**社群（QQ / Bilibili 等）**：見 README。

## 結尾

- 感謝你的貢獻！
