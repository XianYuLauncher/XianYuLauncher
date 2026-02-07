using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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
        private bool _isChatScrollPending = false;
        private UiChatMessage? _lastChatMessage;

        /// <summary>
        /// 用户是否正在手动滚动聊天区域（此时不自动滚动）
        /// </summary>
        private bool _userIsScrollingChat = false;
        private ScrollViewer? _chatScrollViewer;

        public ErrorAnalysisPage()
        {
            ViewModel = App.GetService<ErrorAnalysisViewModel>();
            this.InitializeComponent();
            
            // 订阅LogLines集合变化事件，实现自动滚动到底部
            ViewModel.LogLines.CollectionChanged += LogLines_CollectionChanged;
            
            // 订阅ChatMessages集合变化事件
            ViewModel.ChatMessages.CollectionChanged += ChatMessages_CollectionChanged;

            // 监听 HasChatMessages 变化，占位符切换时刷新滚动
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.HasChatMessages) && ViewModel.HasChatMessages)
                {
                    // 如果 ScrollViewer 还没获取到，等待 ListView 布局完成后再获取
                    if (_chatScrollViewer == null)
                    {
                        void OnLayoutUpdated(object? sender, object args)
                        {
                            _chatScrollViewer = FindScrollViewer(ChatListView);
                            if (_chatScrollViewer != null)
                            {
                                ChatListView.LayoutUpdated -= OnLayoutUpdated;
                                _chatScrollViewer.ViewChanged += ChatScrollViewer_ViewChanged;
                                
                                // 立即滚到底部
                                _chatScrollViewer.ChangeView(null, _chatScrollViewer.ScrollableHeight, null, false);
                            }
                        }
                        
                        ChatListView.LayoutUpdated += OnLayoutUpdated;
                    }
                    else
                    {
                        // ScrollViewer 已经有了，直接刷新
                        System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
                        {
                            this.DispatcherQueue.TryEnqueue(() =>
                            {
                                _chatScrollViewer.UpdateLayout();
                                _chatScrollViewer.ChangeView(null, _chatScrollViewer.ScrollableHeight, null, false);
                            });
                        });
                    }
                }
            };

            // 添加键盘快捷键支持
            LogListView.KeyDown += LogListView_KeyDown;

            // ChatListView 加载完成后获取内部 ScrollViewer
            ChatListView.Loaded += (_, _) =>
            {
                System.Diagnostics.Debug.WriteLine("[滚动调试] ChatListView.Loaded 事件触发");
                _chatScrollViewer = FindScrollViewer(ChatListView);
                if (_chatScrollViewer != null)
                {
                    System.Diagnostics.Debug.WriteLine("[滚动调试] 在 Loaded 事件中成功获取到 ScrollViewer");
                    _chatScrollViewer.ViewChanged += ChatScrollViewer_ViewChanged;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[滚动调试] 在 Loaded 事件中未能获取到 ScrollViewer");
                }
            };
        }

        /// <summary>
        /// 在可视树中查找 ScrollViewer
        /// </summary>
        private static ScrollViewer? FindScrollViewer(Microsoft.UI.Xaml.DependencyObject parent)
        {
            int childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            System.Diagnostics.Debug.WriteLine($"[滚动调试] FindScrollViewer 遍历节点：{parent.GetType().Name}, 子节点数={childCount}");
            
            for (int i = 0; i < childCount; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv)
                {
                    System.Diagnostics.Debug.WriteLine($"[滚动调试] 找到 ScrollViewer！");
                    return sv;
                }
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// 检测用户是否手动滚动了聊天区域
        /// </summary>
        private void ChatScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_chatScrollViewer == null) return;

            // 当滚动结束时判断是否在底部附近（20px 容差）
            if (!e.IsIntermediate)
            {
                var distanceFromBottom = _chatScrollViewer.ScrollableHeight - _chatScrollViewer.VerticalOffset;
                _userIsScrollingChat = distanceFromBottom > 20;
                System.Diagnostics.Debug.WriteLine($"[滚动调试] 用户滚动结束，距离底部={distanceFromBottom:F1}px, 判定为手动滚动={_userIsScrollingChat}");
            }
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
        /// 聊天集合变化时自动滚动
        /// </summary>
        private void ChatMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is UiChatMessage msg)
                        {
                            if (_lastChatMessage != null)
                            {
                                _lastChatMessage.PropertyChanged -= ChatMessage_PropertyChanged;
                            }

                            _lastChatMessage = msg;
                            _lastChatMessage.PropertyChanged += ChatMessage_PropertyChanged;
                        }
                    }
                }

                // 新消息添加时，重置用户滚动状态并滚到底部
                _userIsScrollingChat = false;
                ScrollChatToBottomAsync();
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // 清空时重置状态
                _userIsScrollingChat = false;
                if (_lastChatMessage != null)
                {
                    _lastChatMessage.PropertyChanged -= ChatMessage_PropertyChanged;
                    _lastChatMessage = null;
                }
            }
        }

        private void ChatMessage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(UiChatMessage.Content)) return;

            // 用户手动滚动了，不自动跟随
            if (_userIsScrollingChat)
            {
                System.Diagnostics.Debug.WriteLine("[滚动调试] 用户正在手动滚动，跳过自动滚动");
                return;
            }

            if (_isChatScrollPending) return;

            _isChatScrollPending = true;
            ScrollChatToBottomAsync();
        }

        /// <summary>
        /// 将聊天 ScrollViewer 滚动到真正的底部
        /// </summary>
        private void ScrollChatToBottomAsync()
        {
            System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (_chatScrollViewer != null)
                        {
                            var scrollableHeight = _chatScrollViewer.ScrollableHeight;
                            var currentOffset = _chatScrollViewer.VerticalOffset;
                            System.Diagnostics.Debug.WriteLine($"[滚动调试] 自动滚动：当前位置={currentOffset:F1}, 可滚动高度={scrollableHeight:F1}, 距离底部={scrollableHeight - currentOffset:F1}px");
                            
                            // 使用 ChangeView 滚动到 ScrollableHeight（真正的底部）
                            _chatScrollViewer.ChangeView(null, scrollableHeight, null, true);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[滚动调试] ScrollViewer 为 null，使用 ScrollIntoView 降级");
                            if (ChatListView.Items.Count > 0)
                            {
                                // 降级：ScrollViewer 还没拿到时用 ScrollIntoView
                                ChatListView.ScrollIntoView(ChatListView.Items[ChatListView.Items.Count - 1]);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[滚动调试] 滚动失败：{ex.Message}");
                    }
                    finally
                    {
                        _isChatScrollPending = false;
                    }
                });
            });
        }

        private void ChatInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
                if (!shift.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    if (ViewModel.SendMessageCommand.CanExecute(null))
                    {
                        ViewModel.SendMessageCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        }

        /// <summary>
        /// 处理导航到页面事件
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 每次进入页面先清理修复按钮状态，避免残留
            ViewModel.ResetFixActionState();

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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // 独立窗口还开着时不清空聊天，让用户继续在窗口里对话
            if (!ViewModel.IsFixerWindowOpen)
            {
                _ = ViewModel.ClearChatStateAsync();
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
        /// 弹出独立聊天窗口
        /// </summary>
        private void PopOutChat_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var window = new FixerChatWindow();
            window.Activate();
        }
    }
}