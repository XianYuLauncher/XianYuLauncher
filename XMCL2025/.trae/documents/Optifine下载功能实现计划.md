# Optifine下载功能实现计划

## 1. 功能概述
实现Optifine的下载和安装功能，包括：
- 下载原版Minecraft核心文件
- 下载Optifine核心文件到指定缓存目录
- 创建临时目录结构
- 设置进程级环境变量
- 执行Java命令安装Optifine

## 2. 实现步骤

### 2.1 创建Optifine下载安装方法
在`MinecraftVersionService`中添加`DownloadOptifineVersionAsync`方法，实现以下功能：

### 2.2 下载原版Minecraft核心文件
- 参考`DownloadForgeVersionAsync`方法中的实现
- 下载原版Minecraft JAR文件到指定目录
- 下载原版Minecraft JSON文件

### 2.3 下载Optifine核心文件
- 使用BMCLAPI下载Optifine：`https://bmclapi2.bangbang93.com/optifine/{mcversion}/{type}/{patch}`
- 下载到`%APPDATA%/Local/Packages/XXX/LocalState/cache/Optifine`目录
- 实现进度更新回调

### 2.4 创建临时目录结构
- 创建`%APPDATA%/Local/Packages/XXX/LocalState/cache/.minecraft`目录
- 在临时目录内创建`versions`、`libraries`、`assets`子目录
- 复制下载好的游戏版本目录到临时目录的`versions`内

### 2.5 设置环境变量并执行Java命令
- 设置进程级环境变量`APPDATA`为临时目录的上一级
- 使用`MinecraftVersionService.Processor.cs`中的Java路径选择逻辑
- 执行命令：`JAVA路径 -Duser.home="{临时文件夹的上一级目录}" -cp "{optifine jar文件} optifine.Installer"`
- 捕获并记录执行结果和错误信息

## 3. 文件修改

### 3.1 创建新文件
- `Core/Services/MinecraftVersionService.Optifine.cs`：实现Optifine下载安装功能

### 3.2 相关依赖文件
- `Core/Services/OptifineService.cs`：已实现，用于获取Optifine版本列表
- `Core/Services/MinecraftVersionService.Processor.cs`：用于Java路径选择

## 4. 调试和日志
- 使用`System.Diagnostics.Debug.WriteLine`输出调试信息
- 记录完整的执行上下文和结果
- 保留临时目录文件以便调试

## 5. 错误处理
- 处理网络请求错误
- 处理文件操作错误
- 处理Java命令执行错误
- 提供详细的错误信息

## 6. 与现有功能的集成
- 确保与现有ModLoader选择页面兼容
- 确保与现有版本管理功能兼容
- 确保版本名称格式正确

## 7. 测试要点
- 测试不同Minecraft版本的Optifine下载
- 测试Optifine与Forge的组合下载
- 测试错误处理机制
- 测试进度更新功能

## 8. 预期结果
- 成功下载并安装Optifine
- 生成正确格式的版本名称
- 提供详细的调试信息
- 与现有功能无缝集成