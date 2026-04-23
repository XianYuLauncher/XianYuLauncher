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
- 多范围仅在无法拆分时使用：多个范围用逗号分隔（如 `(version-list, version-management)`），且必须是真的同时修改了多个独立模块。禁止用多范围来概括「改了很多文件」或逃避拆分提交。
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