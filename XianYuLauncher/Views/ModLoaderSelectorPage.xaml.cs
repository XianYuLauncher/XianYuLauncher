using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.ViewModels;
using System.ComponentModel;

namespace XianYuLauncher.Views;

public sealed partial class ModLoaderSelectorPage : Page
{
    public ModLoaderSelectorViewModel ViewModel { get; }
    private readonly DispatcherQueue _dispatcherQueue;

    public ModLoaderSelectorPage()
    {
        ViewModel = App.GetService<ModLoaderSelectorViewModel>();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
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
                try
                {
                    await DownloadProgressDialog.ShowAsync();
                }
                catch (Exception)
                {
                    // 弹窗可能已经打开或被关闭
                }
            }
            else
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        DownloadProgressDialog.Hide();
                    }
                    catch (Exception)
                    {
                        // 弹窗可能已经关闭
                    }
                });
            }
        }
    }
    
    /// <summary>
    /// ModLoader项点击事件处理，用于选择ModLoader并加载版本列表
    /// </summary>
    private async void ModLoaderItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Border border && border.DataContext is XianYuLauncher.Core.Models.ModLoaderItem modLoaderItem)
        {
            await ViewModel.SelectModLoaderCommand.ExecuteAsync(modLoaderItem);
        }
    }
    
    /// <summary>
    /// 取消选择ModLoader事件处理
    /// </summary>
    private void CancelModLoader_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Button button && button.Tag is XianYuLauncher.Core.Models.ModLoaderItem modLoaderItem)
        {
            // 直接调用ViewModel的ClearSelectionCommand
            ViewModel.ClearSelectionCommand.Execute(modLoaderItem);
        }
    }
    
    /// <summary>
    /// 取消选择LiteLoader事件处理
    /// </summary>
    private void CancelLiteLoader_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.IsLiteLoaderSelected = false;
        ViewModel.SelectedLiteLoaderVersion = null;
    }
    
    /// <summary>
    /// Optifine兼容信息文本块加载事件处理
    /// </summary>
    private void CompatibleInfoTextBlock_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.TextBlock textBlock)
        {
            // 获取ListView
            var listView = FindParent<Microsoft.UI.Xaml.Controls.ListView>(textBlock);
            if (listView != null && listView.Tag is XianYuLauncher.Core.Models.ModLoaderItem modLoaderItem)
            {
                // 获取版本名称
                if (textBlock.Tag is string versionName)
                {
                    // 如果是Optifine，获取兼容信息
                    if (modLoaderItem.Name == "Optifine")
                    {
                        var compatibleInfo = modLoaderItem.GetOptifineCompatibleInfo(versionName);
                        textBlock.Text = compatibleInfo;
                    }
                    else
                    {
                        // 不是Optifine，隐藏文本块
                        textBlock.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 查找父元素
    /// </summary>
    /// <typeparam name="T">父元素类型</typeparam>
    /// <param name="element">子元素</param>
    /// <returns>父元素或null</returns>
    private T FindParent<T>(Microsoft.UI.Xaml.DependencyObject element) where T : Microsoft.UI.Xaml.DependencyObject
    {
        while (element != null)
        {
            if (element is T parent)
            {
                return parent;
            }
            element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}