# 集成WebView2显示MC角色3D模型

## 一、技术方案概述

使用 WebView2 + Three.js + skinview3d 库来实现 MC 角色3D模型的渲染。skinview3d 是一个成熟的 Three.js 库，专门用于渲染 MC 皮肤，支持旋转、缩放、动画等功能。

## 二、实施步骤

### 1. 安装必要的 NuGet 包

* 安装 `Microsoft.UI.Xaml.Controls.WebView2` 包

* 确保项目已引用 WinUI 3 框架

### 2. 准备 HTML 和 JavaScript 文件

* 创建 `Skin3DPreview.html` 文件，作为 WebView2 的加载内容

* 集成 `skinview3d` 库（通过 CDN 或本地文件）

* 实现基础的 3D 渲染逻辑

* 添加 JavaScript 函数用于接收 C# 传递的皮肤数据

### 3. 修改 XAML 布局

* 将之前添加的 `Skin3DPreviewContainer` 替换为 WebView2 控件

* 设置 WebView2 的初始属性和事件处理

### 4. 实现 C# 代码

* 在 `CharacterManagementPage.xaml.cs` 中初始化 WebView2

* 实现 `CoreWebView2InitializationCompleted` 事件处理

* 创建 C# 与 JavaScript 通信的桥梁

* 实现皮肤数据传递逻辑

### 5. 测试和调试

* 测试 WebView2 初始化是否成功

* 测试皮肤数据传递是否正常

* 测试 3D 模型渲染效果

* 测试旋转、缩放等交互功能

## 三、关键技术点

### 1. WebView2 初始化

* 处理 `CoreWebView2InitializationCompleted` 事件

* 设置 WebView2 的环境

### 2. C# 与 JavaScript 通信

* 使用 `AddHostObjectToScript` 传递 C# 对象到 JavaScript

* 使用 `ExecuteScriptAsync` 调用 JavaScript 函数

* 使用 `WebMessageReceived` 事件接收 JavaScript 消息

### 3. skinview3d 库使用

* 加载皮肤纹理

* 设置模型类型（经典/纤细）

* 实现动画效果

* 处理用户交互

## 四、资源准备

### 1. 不需要准备 .obj 模型文件

* skinview3d 库内置了 MC 玩家模型

* 只需要提供皮肤纹理文件（PNG 格式）

### 2. 皮肤数据来源

* 离线角色：从本地文件加载

* 在线角色：从 Mojang API 获取

* 外置登录角色：从对应的认证服务器获取

## 五、实施细节

### 1. HTML 文件结构

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <title>MC 皮肤 3D 预览</title>
    <style>
        body { margin: 0; padding: 0; overflow: hidden; }
        #skinContainer { width: 100%; height: 100%; }
    </style>
    <!-- 引入 skinview3d 库 -->
    <script src="https://cdn.jsdelivr.net/npm/skinview3d@3.0.0/bundles/skinview3d.bundle.js"></script>
</head>
<body>
    <div id="skinContainer"></div>
    <script>
        // 初始化 3D 皮肤预览
        const skinViewer = new skinview3d.SkinViewer({
            domElement: document.getElementById('skinContainer'),
            width: 400,
            height: 350,
            skinUrl: '', // 初始为空，后续通过 C# 设置
            capeUrl: ''
        });
        
        // 启用旋转和缩放
        skinViewer.animation = new skinview3d.CompositeAnimation();
        skinViewer.animation.add(skinview3d.WalkingAnimation);
        skinViewer.controls.enableRotate = true;
        skinViewer.controls.enableZoom = true;
        
        // 暴露给 C# 调用的函数
        window.setSkinTexture = function(skinUrl, capeUrl) {
            skinViewer.loadSkin(skinUrl);
            if (capeUrl) {
                skinViewer.loadCape(capeUrl);
            }
        };
        
        window.setAnimation = function(animationName) {
            // 设置动画类型
        };
    </script>
</body>
</html>
```

### 2. C# 代码关键片段

```csharp
// 初始化 WebView2
private async void InitializeWebView2()
{
    // 确保 WebView2 环境已准备好
    if (Skin3DWebView.CoreWebView2 == null)
    {
        await Skin3DWebView.EnsureCoreWebView2Async();
    }
    
    // 加载本地 HTML 文件
    var htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Skin3DPreview.html");
    Skin3DWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
    
    // 设置 WebView2 事件处理
    Skin3DWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
}

// 传递皮肤数据到 JavaScript
private async void UpdateSkinTexture(string skinUrl, string capeUrl = null)
{
    if (Skin3DWebView.CoreWebView2 != null)
    {
        await Skin3DWebView.CoreWebView2.ExecuteScriptAsync(
            $"window.setSkinTexture('{skinUrl}', {capeUrl != null ? $"'{capeUrl}'" : "null"});");
    }
}
```

## 六、预期效果

* 在 CharacterManagementPage.xaml 中显示 350px 高度的 3D 皮肤预览区域

* 支持鼠标旋转和缩放模型

* 支持动画效果（如行走动画）

* 能够实时更新皮肤纹理

* 支持经典和纤细两种模型类型

* 支持披风显示

## 七、注意事项

1. WebView2 需要安装 WebView2 运行时
2. 确保 HTML 和 JS 文件的路径正确
3. 处理好 C# 与 JavaScript 之间的通信
4. 考虑皮肤加载失败的降级处理
5. 优化性能，避免频繁更新导致卡顿

## 八、后续扩展

1. 添加更多动画效果选择
2. 支持模型姿势调整
3. 添加皮肤预览截图功能
4. 支持多人模型对比
5. 支持自定义模型部件

