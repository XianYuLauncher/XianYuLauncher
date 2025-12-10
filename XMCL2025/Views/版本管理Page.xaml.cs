using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class 版本管理Page : Page
{
    public 版本管理ViewModel ViewModel { get; }

    public 版本管理Page()
    {
        ViewModel = App.GetService<版本管理ViewModel>();
        this.DataContext = ViewModel;
        InitializeComponent();
        
        // 监听SelectedVersion变化，更新页面标题
        ViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.SelectedVersion))
            {
                UpdatePageTitle();
            }
        };
        
        // 初始更新标题
        UpdatePageTitle();
    }

    /// <summary>
    /// 更新页面标题
    /// </summary>
    private void UpdatePageTitle()
    {
        if (ViewModel.SelectedVersion != null)
        {
            PageTitle.Text = $"版本管理 - {ViewModel.SelectedVersion.Name}";
            PageSubtitle.Text = $"管理版本 {ViewModel.SelectedVersion.Name} 的组件和资源";
        }
        else
        {
            PageTitle.Text = "版本管理";
            PageSubtitle.Text = "请选择一个版本进行管理";
        }
    }
    
    #region 拖放事件处理
    
    private void ContentArea_DragEnter(object sender, DragEventArgs e)
    {
        // 检查拖放的内容是否包含文件
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            // 显示拖放状态视觉反馈
            ShowDragDropFeedback(true);
        }
    }
    
    private void ContentArea_DragOver(object sender, DragEventArgs e)
    {
        // 检查拖放的内容是否包含文件
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }
    
    private void ContentArea_DragLeave(object sender, DragEventArgs e)
    {
        // 隐藏拖放状态视觉反馈
        ShowDragDropFeedback(false);
    }
    
    private async void ContentArea_Drop(object sender, DragEventArgs e)
    {
        // 隐藏拖放状态视觉反馈
        ShowDragDropFeedback(false);
        
        // 检查拖放的内容是否包含文件
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items != null && items.Count > 0)
            {
                // 调用ViewModel的拖放处理方法
                await ViewModel.HandleDragDropFilesAsync(items);
            }
        }
    }
    
    private void ShowDragDropFeedback(bool isVisible)
    {
        // 显示或隐藏拖放状态视觉反馈覆盖层，使用动画效果
        if (isVisible)
        {
            DragDropOverlay.Opacity = 0;
            DragDropOverlay.Visibility = Visibility.Visible;
            
            // 创建并启动淡入动画
            var fadeInStoryboard = new Storyboard();
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new QuadraticEase()
            };
            Storyboard.SetTarget(fadeInAnimation, DragDropOverlay);
            Storyboard.SetTargetProperty(fadeInAnimation, "Opacity");
            fadeInStoryboard.Children.Add(fadeInAnimation);
            fadeInStoryboard.Begin();
        }
        else
        {
            // 直接隐藏
            DragDropOverlay.Visibility = Visibility.Collapsed;
            DragDropOverlay.Opacity = 0;
        }
    }
    
    #endregion
    
    #region Mod开关事件处理
    
    /// <summary>
    /// 处理Mod启用/禁用开关的Toggled事件
    /// </summary>
    /// <param name="sender">发送事件的ToggleSwitch控件</param>
    /// <param name="e">事件参数</param>
    private async void ModToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            // 获取ToggleSwitch的IsOn值
            bool isOn = toggleSwitch.IsOn;
            
            // 直接从父级Grid获取DataContext
            if (VisualTreeHelper.GetParent(toggleSwitch) is FrameworkElement parentElement && parentElement.DataContext is ViewModels.ModInfo modInfo)
            {
                // 直接调用ViewModel的方法来处理开关状态变化，传递当前IsOn值
                await ViewModel.ToggleModEnabledAsync(modInfo, isOn);
            }
        }
    }
    
    #endregion
}