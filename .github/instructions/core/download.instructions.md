---
applyTo: "XianYuLauncher.Core/Services/**/*.cs,XianYuLauncher.Core/Contracts/Services/**/*.cs,XianYuLauncher.Core/Helpers/**/*.cs,XianYuLauncher/Features/ModDownloadDetail/**/*.cs,XianYuLauncher/ViewModels/VersionManagement/**/*.cs,XianYuLauncher/ViewModels/VersionManagementViewModel.cs,XianYuLauncher/ViewModels/ResourceDownloadViewModel.cs,XianYuLauncher/ViewModels/ModDownloadDetailViewModel.cs,XianYuLauncher/ViewModels/DownloadQueueViewModel.cs,XianYuLauncher/Views/ResourceDownloadPage.xaml.cs,XianYuLauncher/Views/ModDownloadDetailPage.xaml.cs,XianYuLauncher/Views/DownloadQueuePage.xaml.cs,XianYuLauncher/Extensions/DownloadServiceExtensions.cs,XianYuLauncher/Services/**/*Download*.cs,XianYuLauncher/Contracts/Services/**/*Download*.cs"
---

### Project Constraints
以下场景出现时，先询问并获得开发者确认后再执行。

1. **下载接入规范**
   - 新增下载必须接入 `XianYuLauncher.Core/Services/DownloadManager.cs`，并按场景接入 `XianYuLauncher.Core/Services/DownloadTaskManager.cs`。
   - 禁止手写 Http 下载落盘逻辑（如直接 `HttpClient`）。
   - 游戏/社区资源禁止硬编码下载 URL。
   - 必须使用下载源工厂体系（`DownloadSourceFactory` / 下载源接口）。
   - 可多源回退资源优先接入 `XianYuLauncher.Core/Services/FallbackDownloadManager.cs`。
   - 复用现有模式：`SendGetWithFallbackAsync(source => ...)` + `configureRequest` 按源补 Header。
   - 禁止绕过 Fallback 做单源请求（除非兼容分支或开发者确认）。
   - 禁止业务层重复造“手写回退/重试切源”。