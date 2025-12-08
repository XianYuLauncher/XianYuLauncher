using Microsoft.UI.Xaml.Controls;
using XMCL2025.ViewModels;
using System.ComponentModel;

namespace XMCL2025.Views;

public sealed partial class ModLoader选择Page : Page
{
    public ModLoader选择ViewModel ViewModel { get; }

    public ModLoader选择Page()
    {
        ViewModel = App.GetService<ModLoader选择ViewModel>();
        InitializeComponent();
        
        // 监听ViewModel的IsDownloadDialogOpen属性变化
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }
    
    /// <summary>
    /// 监听ViewModel的属性变化，显示或隐藏下载进度弹窗
    /// </summary>
    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsDownloadDialogOpen))
        {
            if (ViewModel.IsDownloadDialogOpen)
            {
                await DownloadProgressDialog.ShowAsync();
            }
            else
            {
                DownloadProgressDialog.Hide();
            }
        }
    }
    
    /// <summary>
    /// 下载进度弹窗关闭按钮点击事件，取消下载
    /// </summary>
    private void DownloadProgressDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 取消下载任务
        // ViewModel会在ConfirmSelectionAsync方法中处理取消逻辑
    }
}