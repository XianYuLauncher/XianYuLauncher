# 实现方案：ModLoader选择页面添加版本名称编辑功能

## 1. UI修改（Views/ModLoader选择Page.xaml）
- 在页面顶部添加一个卡片控件，用于编辑版本名称
- 卡片包含：标题文本（"版本名称"）、文本框（用于输入自定义版本名称）
- 文本框默认值绑定到ViewModel的VersionName属性
- 文本框支持双向绑定，允许用户修改版本名称

## 2. ViewModel修改（ViewModels/ModLoader选择ViewModel.cs）
- 添加`[ObservableProperty] private string _versionName = "";`属性
- 在`OnNavigatedTo`方法中初始化`VersionName = SelectedMinecraftVersion`
- 在`SelectModLoaderAsync`和属性变化事件中更新VersionName，例如：
  - 当选择Fabric时：`VersionName = $"{SelectedMinecraftVersion}-fabric-{SelectedModLoaderVersion}"`
  - 当选择NeoForge时：`VersionName = $"{SelectedMinecraftVersion}-neoforge-{SelectedModLoaderVersion}"`
  - 当选择"无"时：`VersionName = SelectedMinecraftVersion`

## 3. 下载服务接口修改（Core/Contracts/Services/IMinecraftVersionService.cs）
- 更新`DownloadModLoaderVersionAsync`方法签名，添加`string customVersionName`参数
- 更新`DownloadVersionAsync`方法签名，添加`string customVersionName`参数

## 4. 下载服务实现修改（Core/Services/MinecraftVersionService.cs）
- 修改`DownloadModLoaderVersionAsync`方法，使用`customVersionName`作为版本文件夹名称
- 修改`DownloadFabricVersionAsync`方法，使用`customVersionName`创建版本文件夹、jar文件和json文件
- 修改`DownloadNeoForgeVersionAsync`方法，使用`customVersionName`创建版本文件夹、jar文件和json文件
- 修改`DownloadVersionAsync`方法，使用`customVersionName`创建版本文件夹、jar文件和json文件
- 确保在创建`XianYuL.cfg`配置文件时包含自定义版本名称

## 5. 调用点修改（ViewModels/ModLoader选择ViewModel.cs）
- 在`ConfirmSelectionAsync`方法中，将`VersionName`作为参数传递给`DownloadModLoaderVersionAsync`和`DownloadVersionAsync`方法

## 6. 验证和测试
- 确保UI卡片正确显示和更新
- 确保自定义版本名称在切换modloader时自动更新
- 确保下载时使用自定义版本名称创建所有相关文件
- 确保版本配置文件包含正确的自定义版本名称

## 实现要点
- 使用双向绑定确保UI和ViewModel同步
- 实现智能版本名称生成，根据modloader类型自动调整
- 确保向后兼容性，支持用户手动修改版本名称
- 在整个下载流程中统一使用自定义版本名称
- 添加适当的输入验证，确保版本名称符合Minecraft版本命名规范