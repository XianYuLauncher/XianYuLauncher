# 优化设置页面UI计划

## 目标
将当前杂乱的设置页面重构为带有选项卡导航的清晰布局，分为"基础设置"、"Java设置"和"关于"三个类别，保持与资源下载页面一致的设计风格。

## 实现步骤

### 1. 分析现有结构
- 现有设置页面包含个人化设置、Java路径设置、Minecraft路径设置、版本隔离设置和关于信息
- 所有内容当前在一个长列表中，缺乏清晰的组织

### 2. 设计选项卡布局
- 使用与ResourceDownloadPage相同的TabView控件
- 顶部对齐的选项卡导航
- 三个选项卡类别：
  - 基础设置：包含主题设置、Minecraft路径设置、版本隔离设置
  - Java设置：包含Java版本管理、Java选择方式、Java路径设置
  - 关于：包含应用版本信息、描述和隐私条款

### 3. 实现TabView控件
- 添加TabView控件，设置HorizontalAlignment="Stretch"、VerticalAlignment="Stretch"
- 为每个选项卡添加StackPanel Header，包含FontIcon和TextBlock
- 为每个选项卡添加ScrollViewer内容区域

### 4. 重组控件
- 将主题设置、Minecraft路径、版本隔离移动到"基础设置"选项卡
- 将Java版本管理、Java选择方式、Java路径移动到"Java设置"选项卡
- 将版本信息、描述、隐私条款移动到"关于"选项卡

### 5. 保持设计一致性
- 遵循ResourceDownloadPage的间距、排版和配色方案
- 使用相同的控件样式和布局结构
- 确保选项卡切换流畅

### 6. 适配屏幕尺寸
- 确保布局在不同屏幕尺寸下正确显示
- 使用响应式设计，避免硬编码尺寸

## 技术要点

- **TabView实现**：使用Microsoft.UI.Xaml.Controls.TabView
- **布局结构**：Grid + TabView + ScrollViewer + StackPanel
- **设计一致性**：保持与ResourceDownloadPage相同的样式
- **响应式设计**：确保在不同屏幕尺寸下正常显示
- **绑定保持**：确保所有控件的绑定关系不变

## 文件修改

- **SettingsPage.xaml**：重构为选项卡布局

## 预期效果

- 清晰的选项卡导航，便于用户快速找到所需设置
- 与应用其他页面一致的设计风格
- 流畅的选项卡切换体验
- 响应式布局，适配不同屏幕尺寸
- 所有控件保持功能完整