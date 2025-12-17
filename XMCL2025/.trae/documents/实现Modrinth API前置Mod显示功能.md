# 实现Modrinth API前置Mod显示功能

## 1. 核心需求

- 记录Modrinth版本API返回的dependencies字段
- 在下载弹窗中显示前置Mod卡片列表
- 点击卡片可跳转到前置Mod的下载页面
- 显示加载状态，提升交互体验

## 2. 实现步骤

### 2.1 数据模型扩展

1. **添加Dependency类**：表示单个依赖项
   ```csharp
   public class Dependency
   {
       [JsonPropertyName("version_id")]
       public string VersionId { get; set; }
       
       [JsonPropertyName("project_id")]
       public string ProjectId { get; set; }
       
       [JsonPropertyName("file_name")]
       public string FileName { get; set; }
       
       [JsonPropertyName("dependency_type")]
       public string DependencyType { get; set; }
   }
   ```

2. **扩展ModrinthVersion类**：添加Dependencies属性
   ```csharp
   [JsonPropertyName("dependencies")]
   public List<Dependency> Dependencies { get; set; }
   ```

3. **添加DependencyProject类**：表示依赖Mod的详细信息
   ```csharp
   public class DependencyProject
   {
       public string ProjectId { get; set; }
       public string IconUrl { get; set; }
       public string Title { get; set; }
       public string Description { get; set; }
   }
   ```

### 2.2 ViewModel扩展

1. **添加依赖相关属性**：
   - `ObservableCollection<DependencyProject> DependencyProjects`：存储前置Mod列表
   - `bool IsLoadingDependencies`：加载状态标志
   - `ICommand NavigateToDependencyCommand`：跳转到前置Mod页面的命令

2. **实现依赖获取逻辑**：
   - 在打开下载弹窗前调用API获取依赖详情
   - 显示加载指示器
   - 处理API响应，构建DependencyProject列表

3. **添加导航逻辑**：
   - 实现NavigateToDependencyCommand，使用INavigationService跳转到对应Mod页面

### 2.3 UI更新

1. **修改DownloadDialog弹窗**：
   - 添加依赖列表区域
   - 使用卡片式布局显示前置Mod
   - 左侧显示icon，中间显示title和灰色description
   - 添加加载环

2. **添加卡片样式**：
   - 鼠标悬停效果
   - 点击反馈
   - 响应式布局

### 2.4 交互逻辑

1. **打开下载弹窗时**：
   - 检查当前选中Mod版本的dependencies是否为空
   - 非空则开始获取依赖详情，显示加载状态
   - 获取完成后显示依赖卡片列表

2. **点击依赖卡片时**：
   - 调用NavigateToDependencyCommand
   - 跳转到对应Mod的下载页面

## 3. 技术细节

- 使用HttpClient调用Modrinth API
- 实现异步加载，避免UI阻塞
- 使用ObservableCollection实现数据绑定
- 添加适当的错误处理
- 确保UX流畅，显示加载状态

## 4. 测试要点

- 验证依赖列表正确显示
- 验证加载状态显示
- 验证点击导航功能
- 验证无依赖时的正常显示
- 验证API调用失败时的处理

## 5. 文件修改

- `Core/Models/ModrinthSearchResult.cs`：添加Dependency和DependencyProject类，扩展ModrinthVersion
- `ViewModels/ModDownloadDetailViewModel.cs`：添加依赖处理逻辑
- `Views/ModDownloadDetailPage.xaml`：更新DownloadDialog弹窗布局
- `Views/ModDownloadDetailPage.xaml.cs`：添加依赖卡片点击处理

## 6. 预期效果

- 用户打开下载弹窗时，若有前置Mod，会显示加载状态
- 加载完成后，以卡片形式展示所有前置Mod
- 每个卡片显示Mod图标、名称和简介
- 点击卡片可跳转到对应Mod的下载页面
- 无前置Mod时，弹窗正常显示原内容

## 7. 依赖关系

- 需确保INavigationService可用，用于页面跳转
- 需确保HttpClient已正确配置，用于API调用
- 需确保JsonSerializer配置正确，用于API响应解析