using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
}