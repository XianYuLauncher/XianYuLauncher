using Microsoft.UI.Xaml.Controls;
using System;
using XianYuLauncher.Core.Models;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class UpdateDialog : UserControl
{
    /// <summary>
    /// ViewModel
    /// </summary>
    public UpdateDialogViewModel ViewModel { get; private set; }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="updateDialogViewModel">更新弹窗ViewModel</param>
    public UpdateDialog(UpdateDialogViewModel updateDialogViewModel)
    {
        this.InitializeComponent();
        
        // 设置ViewModel
        ViewModel = updateDialogViewModel;
        DataContext = ViewModel;
        
        // 订阅关闭弹窗事件
        ViewModel.CloseDialog += ViewModel_CloseDialog;
    }
    
    /// <summary>
    /// ViewModel关闭弹窗事件处理
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="result">结果</param>
    private void ViewModel_CloseDialog(object sender, bool result)
    {
        // 通知父窗口关闭弹窗
        OnCloseDialog(result);
    }
    
    /// <summary>
    /// 关闭弹窗事件
    /// </summary>
    public event EventHandler<bool> CloseDialog;
    
    /// <summary>
    /// 触发关闭弹窗事件
    /// </summary>
    /// <param name="result">结果</param>
    private void OnCloseDialog(bool result)
    {
        CloseDialog?.Invoke(this, result);
    }
}