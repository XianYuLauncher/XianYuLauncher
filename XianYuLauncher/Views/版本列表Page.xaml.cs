using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XMCL2025.Contracts.Services;
using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class 版本列表Page : Page
{
    private readonly INavigationService _navigationService;

    public 版本列表Page()
    {
        this.DataContext = App.GetService<版本列表ViewModel>();
        _navigationService = App.GetService<INavigationService>();
        InitializeComponent();
        
        // 添加ItemClick事件处理
        VersionsListView.ItemClick += VersionsListView_ItemClick;
        
        // 订阅导出整合包事件
        if (this.DataContext is 版本列表ViewModel viewModel)
        {
            viewModel.ExportModpackRequested += OnExportModpackRequested;
            // 订阅ResourceDirectories集合变化事件
            viewModel.ResourceDirectories.CollectionChanged += ResourceDirectories_CollectionChanged;
        }
    }
    
    /// <summary>
    /// 处理导出整合包请求事件，打开导出整合包弹窗
    /// </summary>
    private async void OnExportModpackRequested(object? sender, 版本列表ViewModel.VersionInfoItem e)
    {
        // 打开导出整合包弹窗
        await ExportModpackDialog.ShowAsync();
    }

    /// <summary>
    /// 版本项点击事件处理，导航至版本管理页面
    /// </summary>
    private void VersionsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is 版本列表ViewModel.VersionInfoItem version)
        {
            // 导航至版本管理页面，传递选中的版本信息
            _navigationService.NavigateTo(typeof(版本管理ViewModel).FullName!, version);
        }
    }

    /// <summary>
    /// 导出整合包弹窗确认按钮点击事件处理
    /// </summary>
    private async void ExportModpackDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (DataContext is 版本列表ViewModel viewModel)
        {
            // 获取选择的导出选项
            var selectedOptions = viewModel.GetSelectedExportOptions();
            
            // 输出到Debug窗口
            System.Diagnostics.Debug.WriteLine("=== 导出整合包选项 ===");
            System.Diagnostics.Debug.WriteLine($"版本: {viewModel.SelectedVersion?.Name}");
            System.Diagnostics.Debug.WriteLine($"选择的导出项数量: {selectedOptions.Count}");
            foreach (var option in selectedOptions)
            {
                System.Diagnostics.Debug.WriteLine($"- {option}");
            }
            System.Diagnostics.Debug.WriteLine("====================");
            
            // 搜索Modrinth获取mod信息
            if (viewModel.SelectedVersion != null)
            {
                System.Diagnostics.Debug.WriteLine("开始搜索Modrinth获取mod信息...");
                var modResults = await viewModel.SearchModrinthForModsAsync(viewModel.SelectedVersion, selectedOptions);
                System.Diagnostics.Debug.WriteLine($"Modrinth搜索完成，找到 {modResults.Count} 个匹配结果");
            }
        }
    }

    /// <summary>
    /// 导出整合包弹窗关闭按钮点击事件处理
    /// </summary>
    private void ExportModpackDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 这里将在后续实现具体的关闭逻辑
    }

    #region 资源目录复选框事件处理
    /// <summary>
    /// 资源目录总复选框点击事件处理（全选）
    /// </summary>
    private void ResourceAll_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is 版本列表ViewModel viewModel)
        {
            // 全选所有资源目录
            foreach (var dir in viewModel.ResourceDirectories)
            {
                dir.IsSelected = true;
            }
        }
    }
    
    /// <summary>
    /// 资源目录总复选框点击事件处理（取消全选）
    /// </summary>
    private void ResourceAll_Unchecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is 版本列表ViewModel viewModel)
        {
            // 取消选择所有资源目录
            foreach (var dir in viewModel.ResourceDirectories)
            {
                dir.IsSelected = false;
            }
        }
    }
    
    /// <summary>
    /// 资源目录总复选框不确定状态事件处理
    /// </summary>
    private void ResourceAll_Indeterminate(object sender, RoutedEventArgs e)
    {
        // 不确定状态由子复选框自动设置，无需额外处理
    }
    
    /// <summary>
    /// 资源目录子复选框选中事件处理
    /// </summary>
    private void ResourceItem_Checked(object sender, RoutedEventArgs e)
    {
        UpdateResourceAllState();
    }
    
    /// <summary>
    /// 资源目录子复选框取消选中事件处理
    /// </summary>
    private void ResourceItem_Unchecked(object sender, RoutedEventArgs e)
    {
        UpdateResourceAllState();
    }
    
    /// <summary>
    /// 展开/折叠按钮点击事件处理
    /// </summary>
    private void ItemExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is 版本列表ViewModel.ResourceItem item)
        {
            // 切换项的展开状态
            item.IsExpanded = !item.IsExpanded;
        }
    }
    
    /// <summary>
    /// 更新资源目录总复选框的状态
    /// </summary>
    private void UpdateResourceAllState()
    {
        if (DataContext is 版本列表ViewModel viewModel)
        {
            // 查找资源目录总复选框
            CheckBox allCheckBox = null;
            // 遍历ResourceDirectoriesStackPanel的子元素，找到资源目录总复选框
            foreach (var child in ResourceDirectoriesStackPanel.Children)
            {
                if (child is Grid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is CheckBox checkBox && checkBox.Content.ToString() == "版本目录资源")
                        {
                            allCheckBox = checkBox;
                            break;
                        }
                    }
                    if (allCheckBox != null)
                    {
                        break;
                    }
                }
            }
            
            if (allCheckBox != null)
            {
                // 计算总选择状态
                bool allSelected = true;
                bool noneSelected = true;
                
                foreach (var item in viewModel.ResourceDirectories)
                {
                    if (item.IsSelected)
                    {
                        noneSelected = false;
                    }
                    else
                    {
                        allSelected = false;
                    }
                    
                    // 如果已经确定不是全选也不是全不选，可以提前退出
                    if (!allSelected && !noneSelected)
                    {
                        break;
                    }
                }
                
                if (noneSelected)
                {
                    allCheckBox.IsChecked = false;
                }
                else if (allSelected)
                {
                    allCheckBox.IsChecked = true;
                }
                else
                {
                    allCheckBox.IsChecked = null;
                }
            }
        }
    }
    
    /// <summary>
    /// 展开/折叠按钮点击事件处理
    /// </summary>
    private void ToggleResourceDirectoriesButton_Click(object sender, RoutedEventArgs e)
    {
        // 切换TreeView的可见性
        if (ResourceDirectoriesTreeView.Visibility == Visibility.Visible)
        {
            // 折叠
            ResourceDirectoriesTreeView.Visibility = Visibility.Collapsed;
            ToggleResourceDirectoriesButton.Content = "▶"; // 右箭头
        }
        else
        {
            // 展开
            ResourceDirectoriesTreeView.Visibility = Visibility.Visible;
            ToggleResourceDirectoriesButton.Content = "▼"; // 下箭头
        }
    }
    
    /// <summary>
    /// 资源目录StackPanel加载事件处理，根据资源目录数量设置可见性
    /// </summary>
    private void ResourceDirectoriesStackPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is 版本列表ViewModel viewModel)
        {
            // 根据资源目录数量设置StackPanel的可见性
            ResourceDirectoriesStackPanel.Visibility = viewModel.ResourceDirectories.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            
            // 初始化TreeView可见性（默认隐藏）
            ResourceDirectoriesTreeView.Visibility = Visibility.Collapsed;
            // 初始化按钮内容
            ToggleResourceDirectoriesButton.Content = "▶"; // 右箭头
        }
    }
    
    /// <summary>
    /// 资源目录列表变化事件处理，根据资源目录数量设置StackPanel的可见性
    /// </summary>
    private void ResourceDirectories_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is 版本列表ViewModel viewModel)
        {
            // 根据资源目录数量设置StackPanel的可见性
            ResourceDirectoriesStackPanel.Visibility = viewModel.ResourceDirectories.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    #endregion
}