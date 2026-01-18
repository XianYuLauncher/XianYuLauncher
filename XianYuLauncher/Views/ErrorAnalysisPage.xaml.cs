using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
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
            
            // 订阅ViewModel的PropertyChanged事件，实现分析结果自动滚动
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // 订阅LogLines集合变化事件，实现自动滚动到底部
            ViewModel.LogLines.CollectionChanged += LogLines_CollectionChanged;
            
            // 添加键盘快捷键支持
            LogListView.KeyDown += LogListView_KeyDown;
        }
        
        /// <summary>
        /// 处理键盘快捷键
        /// </summary>
        private void LogListView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Ctrl+C 复制选中的日志行
            if (e.Key == VirtualKey.C && 
                (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)))
            {
                CopySelectedLogLines();
                e.Handled = true;
            }
            // Ctrl+A 全选
            else if (e.Key == VirtualKey.A && 
                     (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)))
            {
                LogListView.SelectAll();
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// 复制选中的日志行到剪贴板
        /// </summary>
        private void CopySelectedLogLines()
        {
            try
            {
                if (LogListView.SelectedItems.Count > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var item in LogListView.SelectedItems)
                    {
                        if (item is string line)
                        {
                            sb.AppendLine(line);
                        }
                    }
                    
                    var dataPackage = new DataPackage();
                    dataPackage.SetText(sb.ToString());
                    Clipboard.SetContent(dataPackage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制日志失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 日志集合变化时自动滚动到底部
        /// </summary>
        private void LogLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 只在添加新项时滚动
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // 使用防抖机制减少频繁滚动导致的UI卡顿
                if (!_isScrollPending)
                {
                    _isScrollPending = true;
                    
                    // 使用延迟执行滚动操作，避免频繁滚动导致UI卡顿
                    System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                    {
                        // 确保在UI线程上执行滚动操作
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                // 滚动到最后一项
                                if (LogListView.Items.Count > 0)
                                {
                                    LogListView.ScrollIntoView(LogListView.Items[LogListView.Items.Count - 1]);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"滚动到底部失败: {ex.Message}");
                            }
                            finally
                            {
                                _isScrollPending = false;
                            }
                        });
                    });
                }
            }
        }
        
        /// <summary>
        /// 监听ViewModel的PropertyChanged事件，实现分析结果自动滚动
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
                    System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                    {
                        // 确保在UI线程上执行滚动操作
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            // 使用ChangeView方法实现自动滚动到底部
                            KnowledgeBaseScrollViewer.ChangeView(null, double.MaxValue, null);
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
                // 设置游戏崩溃状态为true，触发分析
                ViewModel.SetGameCrashStatus(true);
            }
            // 如果没有导航参数，说明是从 InfoBar 点击"查看日志"按钮进来的
            // 此时日志已经在 ViewModel 中了（因为是 Singleton），不需要清空
            // 只需要确保页面正常显示即可
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
    }
}