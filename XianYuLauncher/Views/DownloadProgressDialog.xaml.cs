using Microsoft.UI.Xaml.Controls;
using System;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class DownloadProgressDialog : UserControl
{
    /// <summary>
    /// ViewModel
    /// </summary>
    public UpdateDialogViewModel ViewModel { get; private set; }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="viewModel">更新弹窗ViewModel</param>
    public DownloadProgressDialog(UpdateDialogViewModel viewModel)
    {
        this.InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}