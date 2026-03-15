# Scripts

开发辅助脚本。

## 开发用

### run.ps1

WinUI 3 打包应用的开发运行脚本，采用与 Visual Studio F5 一致的松散注册策略

**用法**（在仓库根目录执行）：

```powershell
.\scripts\run.ps1              # 打包+注册+启动（完整 F5）
.\scripts\run.ps1 -BuildOnly   # 仅编译验证，最快
.\scripts\run.ps1 -NoLaunch    # 打包+注册，不启动
```

**参数**：

- `-BuildOnly`：仅 msbuild 编译，不打包/注册/启动，适合 AI/CI 快速验证
- `-NoLaunch`：打包并注册，不启动应用

---

## CI 用

None...
