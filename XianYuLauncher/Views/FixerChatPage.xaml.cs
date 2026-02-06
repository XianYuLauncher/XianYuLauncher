using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.System;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

/// <summary>
/// XianYu Fixer 独立聊天页面 — 复用 ErrorAnalysisViewModel（Singleton）
/// </summary>
public sealed partial class FixerChatPage : Page
{
    public ErrorAnalysisViewModel ViewModel { get; }

    private bool _isChatScrollPending;
    private bool _userIsScrollingChat;
    private ScrollViewer? _chatScrollViewer;
    private UiChatMessage? _lastChatMessage;

    public FixerChatPage()
    {
        ViewModel = App.GetService<ErrorAnalysisViewModel>();
        this.InitializeComponent();

        ViewModel.ChatMessages.CollectionChanged += ChatMessages_CollectionChanged;

        ChatListView.Loaded += (_, _) =>
        {
            _chatScrollViewer = FindScrollViewer(ChatListView);
            if (_chatScrollViewer != null)
            {
                _chatScrollViewer.ViewChanged += ChatScrollViewer_ViewChanged;
            }

            // 如果已有消息，滚到底部
            if (ChatListView.Items.Count > 0)
            {
                ScrollChatToBottom();
            }
        };
    }

    // ---- 滚动逻辑（与 ErrorAnalysisPage 一致）----

    private static ScrollViewer? FindScrollViewer(Microsoft.UI.Xaml.DependencyObject parent)
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv) return sv;
            var result = FindScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    private void ChatScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_chatScrollViewer == null) return;
        if (!e.IsIntermediate)
        {
            var distanceFromBottom = _chatScrollViewer.ScrollableHeight - _chatScrollViewer.VerticalOffset;
            _userIsScrollingChat = distanceFromBottom > 20;
        }
    }

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
                            _lastChatMessage.PropertyChanged -= ChatMessage_PropertyChanged;
                        _lastChatMessage = msg;
                        _lastChatMessage.PropertyChanged += ChatMessage_PropertyChanged;
                    }
                }
            }
            _userIsScrollingChat = false;
            ScrollChatToBottom();
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
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
        if (_userIsScrollingChat || _isChatScrollPending) return;
        _isChatScrollPending = true;
        ScrollChatToBottom();
    }

    private void ScrollChatToBottom()
    {
        Task.Delay(50).ContinueWith(_ =>
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (_chatScrollViewer != null)
                        _chatScrollViewer.ChangeView(null, _chatScrollViewer.ScrollableHeight, null, true);
                    else if (ChatListView.Items.Count > 0)
                        ChatListView.ScrollIntoView(ChatListView.Items[ChatListView.Items.Count - 1]);
                }
                catch { }
                finally { _isChatScrollPending = false; }
            });
        });
    }

    // ---- 输入处理 ----

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
}
