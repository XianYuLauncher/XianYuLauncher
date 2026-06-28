### Background
本项目为开源项目，技术栈为 .NET/C# ，专注 WinUI 3 桌面应用。目标：交付健壮、可维护、可验证的代码。

### Core Protocols
1. **反幻觉/反歧义**
   - 指令模糊、上下文缺失或实现路径不唯一时，先提问澄清。
   - 未澄清前，禁止直接改代码。
2. **测试驱动修复 Bug**
   - 禁止盲修。
   - 固定流程：Analyze 根因 -> Fix 修复 -> Verify 验证通过。
3. **上下文优先**
   - 先查文件结构、相关代码、NuGet/依赖，再回答或修改。
   - 禁止基于假设操作。

### Code Style
- 回复语言：中文。
- C# 规范：遵循 C# 10/11+，优先 async/await，严格 Nullable，代码简洁。
- WinUI 3：注意 UI 线程安全。

### Commit
- 使用 Conventional Commits。
- `type` 必须英文（如 `feat`/`fix`/`refactor`/`chore`）。
- `scope` 可选，小写。单一范围按模块/组件填写（如 `(protocol)`、`(mod-download-detail-viewmodel)`）。
- 多范围仅在无法拆分时使用：多个范围用逗号分隔（如 `(version-list, version-management)`，注意逗号右侧加空格），且必须是真的同时修改了多个独立模块。禁止用多范围来概括「改了很多文件」或逃避拆分提交。
- 标题与正文使用中文，描述简洁清晰；重大变更补充背景与注意事项。

示例：
```text
feat(protocol): 将 xianyulauncher:// URI 协议激活抽离为可扩展 Protocol 模块

- 新增 Features/Protocol 模块：Parser、Dispatcher、Handler 分层架构
- 抽取 ProtocolPathSecurityHelper、ProtocolQueryStringHelper 至 Core
```

### Build & Test
- 主项目：`msbuild XianYuLauncher/XianYuLauncher.csproj -p:Configuration=Debug -p:Platform=x64 -nologo -v:minimal`（正常输出 warning，同时避免 WinUI/MSIX 生成项长路径刷屏。）
- Core：`dotnet build XianYuLauncher.Core/XianYuLauncher.Core.csproj`
- 测试：`dotnet test <测试项目.csproj>`，禁止 `runTest` 和 `--no-build`。

### Security
- 处理敏感信息时，禁止直接暴露在代码或日志中，优先询问开发者。
- 除开发者需求外，不要写入任何文档在项目中。

### Project Constraints
以下场景出现时，先询问并获得开发者确认后再执行。

### 弹窗规范

1. **入口**：新增弹窗统一走 `XianYuLauncher/Features/Dialogs/` 下的服务（`ICommonDialogService`、`IApplicationDialogService`、`IResourceDialogService` 等），由 `IContentDialogHostService` 统一展示动画与行为。
2. **禁止**：在页面 XAML 或 ViewModel 中硬编码 `ContentDialog` / 内联用户可见中英文字面量作为 `Show*DialogAsync` 的 title、message、按钮参数。
3. **本地化**：
   - 静态文案使用 `Strings/zh-cn`、`Strings/en-us` 与 `Strings/zh-tw` 的 `Resources.resw`，通过 `"ResourceKey".GetLocalized()` 或 `GetLocalized(key, args)` 解析。
   - 弹窗专用键：`Dialog_{Feature}_{Part}`；跨模块消息：`Msg_{Scenario}` / `Msg_{Scenario}_Format`（动态段用 `{0}` 包裹 `ex.Message` 等，不要求异常原文英文化）。
4. **默认按钮**：`ShowMessageDialogAsync` / `ShowConfirmationDialogAsync` 等可不传按钮文本（服务内默认 `Dialog_OK` / `Dialog_Yes` / `Dialog_No` / `Dialog_Cancel`）。

### UI Design
- 遵循 Fluent Design，现代、简洁、原生 Windows 风格。
- 可用 Community Toolkit，但涉及新实现先询问开发者。

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
