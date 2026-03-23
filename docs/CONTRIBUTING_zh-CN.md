![XianYu Launcher 贡献指南](/XianYuLauncher/Assets/ContributingHero_zh.png)

[English](../CONTRIBUTING.md) | [简体中文](CONTRIBUTING_zh-CN.md) | [繁體中文](CONTRIBUTING_zh-TW.md)

# 参与贡献

感谢你对 XianYu Launcher 的关注。本文说明如何提交 Issue 与参与代码贡献；前半部分为 Issue 填写说明，后半部分为 PR 与本地构建相关约定。

---

## 如何提交 Issue

### 从哪开始

1. 打开本仓库 GitHub 页的 **Issues** → **New issue**。
2. **请优先选用模板**（比空 Issue 更容易被处理）：
   - **漏洞反馈**：崩溃、错误行为、与预期不符的缺陷。
   - **功能建议**：新功能、体验优化、产品向想法。

模板在网页上选对应类型即可，系统会带好标题前缀和标签提示。

### 提 Bug 时多写一点什么

- **启动器版本**、**Windows 版本**、相关 **ModLoader 类型/版本**（若与游戏异常有关）。
- **复现步骤**：按 1、2、3 写清楚「点了什么、先后次序」，能稳定复现最好。
- **日志**：模板里已说明从 **设置 → 关于 → 快速动作 → 打开日志目录** 取日志；把**相关片段**贴进 Issue（用代码块包起来）或上传文件，比只有一句崩溃了有用得多。
- **截图**：有弹窗、界面异常时尽量带图。

若暂时不确定算 Bug 还是建议，任选更接近的一类即可，开发者会再归类。

### 提功能建议时多写一点什么

- **解决什么痛点**：现在哪里不舒服、多花了什么时间。
- **你期望的行为或界面**：尽量具体，若有参考（别的软件、草图）可放在「补充材料」里。

### 其它习惯

- 多个无关问题请拆成多个 Issue，方便跟踪和关闭。
- 避免重复开已讨论过的相同问题，可以先看一遍 Issue 列表。
- 语言均可；关键信息写清楚比措辞完美更重要。

---

## 代码贡献（PR）

准备直接改仓库时，**架构约束、下载/弹窗等规范**请以 **`.github/copilot-instructions.md`** 为准（与维护者、Copilot 共用的说明）。

### 提交说明

采用 **Conventional Commits**：

- **`type` 使用英文**：如 `feat`、`fix`、`refactor`、`chore` 等。
- **`scope` 可选**，如 `(protocol)`、`(ModDownloadDetailViewModel)`。
- **标题与正文均可**，按常用语言自行选择即可，简洁说明**做了什么、为什么**；较大变更请在正文中补充背景与迁移注意点。

示例：

```text
fix(setting): 修复设置项不生效

- 将错误的属性读取键改为正确键
- 同步各个调用点
```

### 提交流程建议

1. 从 `main` 新建功能分支。
2. 保持改动 **聚焦**：一个 PR 尽量解决一类问题，避免无关格式化或大范围重排。
3. 修复 Bug 时，若条件允许，请在 PR 描述里关联 Issue，并写清 **验证方式**。
4. 提交 PR 后关注 Review；有较大设计分歧时，**先开 Issue 对齐**再大规模改代码会更舒服。

---

## 环境与构建

若本地已具备 WinUI / 桌面开发环境，可略过本节；需要与仓库说明对齐时可参考。

- **系统**：Windows 10 1809 (17763) 或更高版本（与 README 一致）。
- **工具**：**.NET 10.0**；开发 WinUI 建议使用带 **.NET 桌面开发**、**WinUI 应用程序开发**、**MSBuild** 等负载的 **Visual Studio 2026 及以上版本**（以能正常打开本解决方案为准）。

**常用命令**（仓库根目录）：

```bash
# 主应用（WinUI）
msbuild XianYuLauncher/XianYuLauncher.csproj -p:Configuration=Debug -p:Platform=x64 -nologo -v:minimal

# Core
dotnet build XianYuLauncher.Core/XianYuLauncher.Core.csproj

# 测试（将 <测试项目.csproj> 换成实际要测的测试项目）
dotnet test <测试项目.csproj>
```

---

## 其它入口

- **文档站点**、**社区（QQ / Bilibili 等）**：见 README。

## 结尾

- 感谢你的贡献！
