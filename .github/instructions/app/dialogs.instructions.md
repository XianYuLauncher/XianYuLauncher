---
applyTo: "XianYuLauncher/**/*.xaml,XianYuLauncher/Views/**/*.cs,XianYuLauncher/ViewModels/**/*.cs,XianYuLauncher/Features/Dialogs/**/*.cs,XianYuLauncher/Services/**/*.cs"
---

### Project Constraints
以下场景出现时，先询问并获得开发者确认后再执行。

### 弹窗规范

1. **入口**：新增弹窗统一走 `XianYuLauncher/Features/Dialogs/` 下的服务（`ICommonDialogService`、`IApplicationDialogService`、`IResourceDialogService` 等），由 `IContentDialogHostService` 统一展示动画与行为。
2. **禁止**：在页面 XAML 或 ViewModel 中硬编码 `ContentDialog` / 内联用户可见中英文字面量作为 `Show*DialogAsync` 的 title、message、按钮参数。
3. **本地化**：
   - 静态文案使用 `Strings/zh-cn` 与 `Strings/en-us` 的 `Resources.resw`，通过 `"ResourceKey".GetLocalized()` 或 `GetLocalized(key, args)` 解析。
   - 弹窗专用键：`Dialog_{Feature}_{Part}`；跨模块消息：`Msg_{Scenario}` / `Msg_{Scenario}_Format`（动态段用 `{0}` 包裹 `ex.Message` 等，不要求异常原文英文化）。
4. **默认按钮**：`ShowMessageDialogAsync` / `ShowConfirmationDialogAsync` 等可不传按钮文本（服务内默认 `Dialog_OK` / `Dialog_Yes` / `Dialog_No` / `Dialog_Cancel`）。
