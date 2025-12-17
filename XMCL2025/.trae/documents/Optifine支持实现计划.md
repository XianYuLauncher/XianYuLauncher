# Optifine支持实现计划

## 1. 项目结构和依赖分析

### 1.1 现有项目结构
- **ModLoader选择页面**：用于选择不同的ModLoader
- **ModLoaderItem类**：表示单个ModLoader项，包含名称、版本列表等属性
- **ModLoader选择ViewModel**：处理ModLoader的加载、选择和下载逻辑
- **ForgeService**：用于获取Forge版本列表

### 1.2 新增依赖和服务
- **OptifineService**：用于从BMCLAPI获取Optifine版本列表
- **Optifine版本模型**：用于解析BMCLAPI返回的Optifine版本数据

## 2. 实现步骤

### 2.1 创建Optifine服务和模型

#### 2.1.1 创建Optifine版本模型
```csharp
// Core/Services/OptifineVersion.cs
public class OptifineVersion
{
    public string Type { get; set; }
    public string Patch { get; set; }
    public string Forge { get; set; }
}
```

#### 2.1.2 创建OptifineService
```csharp
// Core/Services/OptifineService.cs
public class OptifineService
{
    private readonly HttpClient _httpClient;
    
    public OptifineService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<List<OptifineVersion>> GetOptifineVersionsAsync(string minecraftVersion)
    {
        // 从BMCLAPI获取Optifine版本列表
        string url = $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersion}";
        string response = await _httpClient.GetStringAsync(url);
        
        // 解析JSON数据
        var optifineVersions = JsonSerializer.Deserialize<List<OptifineVersion>>(response);
        return optifineVersions ?? new List<OptifineVersion>();
    }
}
```

### 2.2 修改ModLoaderItem类

- 添加Optifine相关属性
- 支持Optifine版本和兼容性检查

```csharp
// Core/Models/ModLoaderItem.cs
public class ModLoaderItem : INotifyPropertyChanged
{
    // 现有属性...
    
    // Optifine相关属性
    [ObservableProperty]
    private ObservableCollection<OptifineVersion> _optifineVersions = new();
    
    [ObservableProperty]
    private OptifineVersion? _selectedOptifineVersion;
    
    // 是否显示Optifine选项
    public bool ShowOptifine => Name == "Forge";
}
```

### 2.3 修改ModLoader选择ViewModel

- 添加OptifineService依赖
- 实现Optifine版本加载逻辑
- 实现Optifine与Forge版本兼容性检查

```csharp
// ViewModels/ModLoader选择ViewModel.cs
public partial class ModLoader选择ViewModel : ObservableRecipient, INavigationAware
{
    private readonly OptifineService _optifineService;
    
    public ModLoader选择ViewModel(/* 现有依赖 */, OptifineService optifineService)
    {
        // 现有依赖初始化...
        _optifineService = optifineService;
    }
    
    // 加载Optifine版本列表
    private async Task LoadOptifineVersionsAsync(ModLoaderItem modLoaderItem, CancellationToken cancellationToken)
    {
        if (modLoaderItem.Name != "Forge") return;
        
        try
        {
            var optifineVersions = await _optifineService.GetOptifineVersionsAsync(SelectedMinecraftVersion);
            modLoaderItem.OptifineVersions.Clear();
            foreach (var version in optifineVersions)
            {
                modLoaderItem.OptifineVersions.Add(version);
            }
        }
        catch (Exception ex)
        {
            // 处理异常
        }
    }
    
    // 在ExpandModLoaderAsync中调用LoadOptifineVersionsAsync
    // 确保Forge版本加载完成后加载Optifine版本
}
```

### 2.4 修改ModLoader选择Page.xaml

- 添加Optifine UI元素
- 实现Optifine版本列表和选择
- 实现Optifine与Forge版本兼容性检查

```xaml
<!-- Views/ModLoader选择Page.xaml -->
<Expander.Content>
    <Grid Padding="16,12" MinHeight="100" HorizontalAlignment="Stretch">
        <!-- 现有加载指示器和Forge版本列表... -->
        
        <!-- Optifine选择区域 (仅当选择Forge时显示) -->
        <StackPanel 
            Visibility="{x:Bind ShowOptifine, Mode=OneWay}"
            Margin="0,16,0,0">
            <TextBlock Text="Optifine版本:" FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8" />
            <ListView
                ItemsSource="{x:Bind OptifineVersions, Mode=OneWay}"
                SelectedItem="{x:Bind SelectedOptifineVersion, Mode=TwoWay}"
                SelectionMode="Single"
                Background="Transparent"
                BorderThickness="0"
                MaxHeight="200">
                <ListView.ItemTemplate>
                    <DataTemplate x:DataType="core:OptifineVersion">
                        <Grid>
                            <Border Padding="12,8" Background="{ThemeResource CardBackgroundFillColorSecondaryBrush}" CornerRadius="6" Margin="0,4,0,4">
                                <StackPanel>
                                    <TextBlock 
                                        Text="{x:Bind $($parent.Name) + '_' + $parent.Patch}" 
                                        FontSize="15" />
                                    <TextBlock 
                                        Text="Forge兼容: {x:Bind Forge}" 
                                        FontSize="12" 
                                        Foreground="{ThemeResource TextFillColorSecondary}" 
                                        Margin="0,4,0,0" />
                                </StackPanel>
                            </Border>
                        </Grid>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>
        </StackPanel>
    </Grid>
</Expander.Content>
```

### 2.5 实现兼容性检查

- 在ModLoaderItem类中添加兼容性检查逻辑
- 确保只有与当前选择的Forge版本兼容的Optifine版本才能被选择

```csharp
// Core/Models/ModLoaderItem.cs
public partial class ModLoaderItem
{
    // 计算属性：当前选择的Forge版本是否与Optifine版本兼容
    public bool IsOptifineCompatible(OptifineVersion optifineVersion)
    {
        if (Name != "Forge" || string.IsNullOrEmpty(SelectedVersion)) return false;
        
        // 检查Forge版本兼容性
        // 逻辑：如果Optifine版本的Forge字段为"Forge N/A"或包含当前选择的Forge版本，则兼容
        return optifineVersion.Forge == "Forge N/A" || optifineVersion.Forge.Contains(SelectedVersion);
    }
}
```

### 2.6 确保Optifine只能与Forge一起使用

- 在ViewModel中添加逻辑，确保当选择其他ModLoader时，Optifine选择被清除
- 在UI中添加逻辑，确保只有当选择Forge时，Optifine选项才显示

```csharp
// ViewModels/ModLoader选择ViewModel.cs
partial void OnSelectedModLoaderItemChanged(ModLoaderItem? oldValue, ModLoaderItem? newValue)
{
    // 现有逻辑...
    
    // 当切换到非Forge ModLoader时，清除Optifine选择
    if (newValue?.Name != "Forge")
    {
        // 清除Optifine相关选择
    }
}
```

## 3. 测试和验证

### 3.1 功能测试
- [ ] Optifine版本列表正确加载
- [ ] Optifine只能与Forge一起使用
- [ ] 只有兼容的Optifine版本才能被选择
- [ ] Optifine版本显示格式正确（type_patch）
- [ ] 切换ModLoader时，Optifine选择被正确清除

### 3.2 兼容性测试
- [ ] 不同Minecraft版本的Optifine版本列表正确加载
- [ ] Forge版本兼容性检查正确
- [ ] 不兼容的Optifine版本被正确禁用

## 4. 代码优化和性能考虑

### 4.1 性能优化
- 缓存Optifine版本列表，避免重复请求
- 异步加载Optifine版本列表，避免UI卡顿
- 只在选择Forge时加载Optifine版本列表

### 4.2 代码质量
- 遵循现有代码风格和命名规范
- 添加适当的注释和文档
- 确保代码可维护性和扩展性

## 5. 实现优先级

1. **创建Optifine服务和模型**：完成数据获取和解析基础
2. **修改ModLoaderItem类**：添加Optifine相关属性
3. **修改ModLoader选择ViewModel**：实现Optifine版本加载和兼容性检查
4. **修改ModLoader选择Page.xaml**：添加Optifine UI元素
5. **测试和验证**：确保所有功能正常工作

## 6. 预期效果

- 用户进入ModLoader选择页面后，可以选择Forge
- 选择Forge后，显示Optifine版本列表
- 只有与当前Forge版本兼容的Optifine版本才能被选择
- Optifine版本显示格式为：type_patch（例如：HD_U_J7_pre10）
- 切换到其他ModLoader时，Optifine选择被清除
- 所有功能符合预期，没有语法错误和UI问题