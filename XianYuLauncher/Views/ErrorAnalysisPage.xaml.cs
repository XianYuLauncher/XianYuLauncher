using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using XMCL2025.ViewModels;

namespace XMCL2025.Views
{
    /// <summary>
    /// 错误分析系统页面
    /// </summary>
    public sealed partial class ErrorAnalysisPage : Page
    {
        public ErrorAnalysisViewModel ViewModel { get; }
        private bool _isScrollPending = false;

        public ErrorAnalysisPage()
        {
            ViewModel = App.GetService<ErrorAnalysisViewModel>();
            this.InitializeComponent();
        }

        /// <summary>
        /// 处理导航到页面事件
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 接收导航参数（如果有）
            if (e.Parameter is Tuple<string, List<string>, List<string>> logData)
            {
                ViewModel.SetLogData(logData.Item1, logData.Item2, logData.Item3);
            }
        }

        /// <summary>
        /// 返回按钮点击事件
        /// </summary>
        private void BackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        /// <summary>
        /// 日志文本变化事件，自动滚动到底部
        /// 使用防抖机制减少频繁滚动导致的UI卡顿
        /// </summary>
        private void LogTextBox_TextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e)
        {
            // 如果已经有滚动操作在等待，就不再重复处理
            if (!_isScrollPending)
            {
                _isScrollPending = true;
                
                // 使用延迟执行滚动操作，避免频繁滚动导致UI卡顿
                Task.Delay(100).ContinueWith(_ =>
                {
                    // 确保在UI线程上执行滚动操作
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        // 使用ChangeView方法实现自动滚动到底部
                        // 将垂直偏移量设置为最大值，实现滚动到底部的效果
                        LogScrollViewer.ChangeView(null, double.MaxValue, null);
                        _isScrollPending = false;
                    });
                });
            }
        }
    }
}