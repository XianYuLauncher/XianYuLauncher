using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views
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
            
            // 订阅ViewModel的PropertyChanged事件，实现AI分析结果自动滚动
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
        
        /// <summary>
        /// 监听ViewModel的PropertyChanged事件，实现AI分析结果自动滚动
        /// </summary>
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 检查是否是AiAnalysisResult属性发生变化
            if (e.PropertyName == nameof(ViewModel.AiAnalysisResult))
            {
                // 使用防抖机制减少频繁滚动导致的UI卡顿
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
                            AiScrollViewer.ChangeView(null, double.MaxValue, null);
                            _isScrollPending = false;
                        });
                    });
                }
            }
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
                // 设置游戏崩溃状态为true，触发AI分析
                ViewModel.SetGameCrashStatus(true);
            }
            else
            {
                // 没有导航参数时，重置日志和AI分析结果
                // 这是为了处理"启动游戏实时日志"功能自动导航到页面的情况
                ViewModel.SetLogData(string.Empty, new List<string>(), new List<string>());
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
        
        /// <summary>
        /// 处理Markdown文本中的链接点击事件
        /// </summary>
        private void OnMarkdownLinkClicked(object sender, CommunityToolkit.WinUI.UI.Controls.LinkClickedEventArgs e)
        {
            try
            {
                // 打开链接
                var uri = new Uri(e.Link);
                _ = Windows.System.Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"无法打开链接: {ex.Message}");
            }
        }
    }
}