# 实现版本管理页面的Mod管理功能

## 1. 数据结构设计
- 创建 `ModInfo` 类，用于存储单个mod的详细信息
  - 属性：`Name`（mod名称）、`FilePath`（完整路径）、`IsEnabled`（是否启用）、`FileName`（文件名）

## 2. ViewModel 实现
- 修改 `Mods` 属性类型：从 `ObservableCollection<string>` 改为 `ObservableCollection<ModInfo>`
- 更新 `LoadModsAsync` 方法：
  - 使用 `GetVersionSpecificPath` 获取版本特定的mods文件夹路径
  - 扫描文件夹中的所有.jar文件
  - 检查文件名是否以 `.disabled` 结尾来判断是否禁用
  - 创建 `ModInfo` 对象并添加到列表中
- 添加 `DeleteModAsync` 方法：
  - 根据 `ModInfo` 对象获取文件路径
  - 删除对应的mod文件
  - 从列表中移除该mod
- 添加 `ToggleModEnabledAsync` 方法：
  - 切换mod的 `IsEnabled` 状态
  - 重命名文件，添加或移除 `.disabled` 后缀
  - 更新UI显示
- 添加 `RefreshModListAsync` 方法：重新加载mod列表

## 3. UI 设计与实现
- 修改 `ModListView` 的 `ItemTemplate`：
  - 显示mod名称
  - 添加启用/禁用开关（ToggleSwitch）
  - 添加删除按钮
  - 添加视觉标识（如不同背景色或图标）表示启用/禁用状态
- 添加空列表提示的绑定：当mod列表为空时显示提示信息
- 优化布局：确保操作按钮排列合理，视觉层次清晰

## 4. 交互体验优化
- 操作反馈：添加状态消息提示操作结果
- 实时更新：操作后立即刷新mod列表
- 视觉反馈：启用/禁用状态有明确的视觉区分
- 操作确认：删除操作可考虑添加确认提示（可选）

## 5. 技术实现要点
- 使用 `Path` 类处理文件路径
- 使用 `File` 类进行文件操作（删除、重命名）
- 使用 `ObservableCollection` 实现数据绑定和实时更新
- 使用 `RelayCommand` 实现命令绑定
- 使用 `ToggleSwitch` 控件实现启用/禁用切换
- 使用 `Button` 控件实现删除功能

## 6. 代码结构调整
- 在 `版本管理ViewModel.cs` 中添加 `ModInfo` 类
- 更新 `LoadVersionDataAsync` 方法，确保正确加载mod数据
- 更新 `OnNavigatedTo` 方法，确保导航时正确加载数据

## 7. 测试与验证
- 确保mod列表能正确显示
- 确保删除功能正常工作
- 确保启用/禁用切换功能正常工作
- 确保操作后UI实时更新
- 确保空列表提示正确显示

通过以上步骤，我将实现一个功能完整、界面美观的mod管理模块，为后续其他管理功能的开发奠定基础。