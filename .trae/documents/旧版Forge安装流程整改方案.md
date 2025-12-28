# 旧版Forge安装流程整改方案

## 1. 问题分析

当前项目仅实现了新版Forge安装流程，缺少对**微旧版**和**旧版**Forge的支持。需要修改版本判断逻辑，并实现这两种版本的安装流程。

## 2. 整改目标

* 支持三种Forge版本类型的安装：新版、微旧版、旧版

* 增加详细的调试输出，方便开发人员调试

* 保持与现有代码的兼容性

* 实现完整的安装流程，确保各种Forge版本都能正常安装

## 3. 版本类型定义

| 版本类型 | 特征                                                           |
| ---- | ------------------------------------------------------------ |
| 新版   | 有version.json，install\_profile.json中processors字段有项           |
| 微旧版  | 有version.json，install\_profile.json中processors字段无项           |
| 旧版   | 无version.json，install\_profile.json中有install字段，无processors字段 |

## 4. 整改方案

### 4.1 修改版本判断逻辑

**文件**：`MinecraftVersionService.Forge.cs`

* **移除**：根据version.json是否存在判断版本类型的逻辑

* **新增**：基于install\_profile.json的详细判断逻辑

* **顺序**：先判断旧版，再判断微旧版，最后判断新版

### 4.2 实现三种版本的安装流程

#### 4.2.1 旧版Forge安装流程

1. **读取install\_profile.json**：获取versionInfo和install字段
2. **合并JSON**：将versionInfo与原版JSON合并
3. **处理universal包**：

   * 从install.path获取universal包的maven路径

   * 转换为本地libraries路径

   * 从install.filePath获取universal包文件名

   * 从installer.jar中提取universal包到指定路径
4. **保存合并后的JSON**
5. **清理临时文件**

#### 4.2.2 微旧版Forge安装流程

1. **跳过processors执行**：install\_profile.json中processors字段无项
2. **其他流程**：与新版相同

#### 4.2.3 新版Forge安装流程

* 保持现有实现不变

### 4.3 增加详细调试输出

* **进入流程时**：输出当前Forge版本类型

* **关键步骤**：输出详细的调试信息

* **文件操作**：输出文件路径和操作结果

* **进度更新**：输出当前进度和操作内容

## 5. 具体实现步骤

### 5.1 修改DownloadForgeVersionAsync方法

1. **修改版本判断逻辑**
2. **添加旧版安装流程分支**
3. **添加微旧版安装流程分支**
4. **增加调试输出**

### 5.2 实现旧版Forge安装逻辑

1. **解析install\_profile.json中的install字段**
2. **提取versionInfo并合并**
3. **处理universal包提取**
4. **保存合并后的JSON**

### 5.3 实现微旧版Forge安装逻辑

1. **检测processors字段为空**
2. **跳过处理器执行**
3. **执行其他安装步骤**

### 5.4 增加调试输出

* 在关键步骤添加详细的Debug.WriteLine输出

* 输出当前执行的流程类型

* 输出文件操作的详细信息

## 6. 代码修改范围

| 文件                               | 修改内容                         |
| -------------------------------- | ---------------------------- |
| MinecraftVersionService.Forge.cs | 修改版本判断逻辑，实现旧版和微旧版安装流程，增加调试输出 |
| 相关辅助方法                           | 可能需要添加处理universal包的辅助方法      |

## 7. 预期效果

* ✅ 支持三种Forge版本类型的安装

* ✅ 详细的调试输出，方便开发人员调试

* ✅ 保持与现有代码的兼容性

* ✅ 完整的安装流程，确保各种Forge版本都能正常安装

## 8. 风险评估

* **低风险**：修改范围集中，不影响其他功能

* **兼容性**：保持与现有代码的兼容性

* **测试**：需要测试各种Forge版本的安装流程

## 9. 验收标准

* ✅ 新版Forge安装流程正常工作

* ✅ 微旧版Forge安装流程正常工作

* ✅ 旧版Forge安装流程正常工作

* ✅ 详细的调试输出

* ✅ 无编译错误

* ✅ 安装后的Forge版本能正常启动

