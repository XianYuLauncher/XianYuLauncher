---
applyTo: "XianYuLauncher/**/*.xaml,XianYuLauncher/Views/**/*.cs,XianYuLauncher/ViewModels/**/*.cs,XianYuLauncher/Features/Dialogs/**/*.cs,XianYuLauncher/Services/**/*.cs"
---

### Project Constraints
以下场景出现时，先询问并获得开发者确认后再执行。

2. **弹窗规范**
   - 新增弹窗统一走 `XianYuLauncher/Services/DialogService.cs`。
   - 禁止在页面 XAML 硬编码弹窗逻辑。