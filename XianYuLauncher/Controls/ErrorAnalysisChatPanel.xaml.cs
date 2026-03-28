using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Controls;

public sealed partial class ErrorAnalysisChatPanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ErrorAnalysisViewModel),
        typeof(ErrorAnalysisChatPanel),
        new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty IsComposerEnabledProperty = DependencyProperty.Register(
        nameof(IsComposerEnabled),
        typeof(bool),
        typeof(ErrorAnalysisChatPanel),
        new PropertyMetadata(true, OnComposerStateChanged));

    public static readonly DependencyProperty ShowEmptyPlaceholderProperty = DependencyProperty.Register(
        nameof(ShowEmptyPlaceholder),
        typeof(bool),
        typeof(ErrorAnalysisChatPanel),
        new PropertyMetadata(false, OnPlaceholderSettingsChanged));

    public static readonly DependencyProperty EmptyPlaceholderTextProperty = DependencyProperty.Register(
        nameof(EmptyPlaceholderText),
        typeof(string),
        typeof(ErrorAnalysisChatPanel),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessagesMaxHeightProperty = DependencyProperty.Register(
        nameof(MessagesMaxHeight),
        typeof(double),
        typeof(ErrorAnalysisChatPanel),
        new PropertyMetadata(double.PositiveInfinity));

    private readonly IUiDispatcher _uiDispatcher;
    private bool _isChatScrollPending;
    private bool _userIsScrollingChat;
    private ScrollViewer? _chatScrollViewer;
    private UiChatMessage? _lastChatMessage;
    private ErrorAnalysisViewModel? _attachedViewModel;

    public ErrorAnalysisViewModel? ViewModel
    {
        get => (ErrorAnalysisViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public bool ShowEmptyPlaceholder
    {
        get => (bool)GetValue(ShowEmptyPlaceholderProperty);
        set => SetValue(ShowEmptyPlaceholderProperty, value);
    }

    public bool IsComposerEnabled
    {
        get => (bool)GetValue(IsComposerEnabledProperty);
        set => SetValue(IsComposerEnabledProperty, value);
    }

    public string EmptyPlaceholderText
    {
        get => (string)GetValue(EmptyPlaceholderTextProperty);
        set => SetValue(EmptyPlaceholderTextProperty, value);
    }

    public double MessagesMaxHeight
    {
        get => (double)GetValue(MessagesMaxHeightProperty);
        set => SetValue(MessagesMaxHeightProperty, value);
    }

    public ErrorAnalysisChatPanel()
    {
        InitializeComponent();
        _uiDispatcher = App.GetService<IUiDispatcher>();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ChatListView.Loaded += OnChatListViewLoaded;
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErrorAnalysisChatPanel panel)
        {
            panel.DetachViewModelHandlers(e.OldValue as ErrorAnalysisViewModel);
            panel.AttachViewModelHandlers(e.NewValue as ErrorAnalysisViewModel);
            panel.UpdatePlaceholderState();
            panel.UpdateComposerState();
        }
    }

    private static void OnPlaceholderSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErrorAnalysisChatPanel panel)
        {
            panel.UpdatePlaceholderState();
        }
    }

    private static void OnComposerStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErrorAnalysisChatPanel panel)
        {
            panel.UpdateComposerState();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModelHandlers(ViewModel);
        UpdatePlaceholderState();
        UpdateComposerState();
        _ = ScrollChatToBottomAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModelHandlers(ViewModel);

        if (_chatScrollViewer != null)
        {
            _chatScrollViewer.ViewChanged -= ChatScrollViewer_ViewChanged;
            _chatScrollViewer = null;
        }
    }

    private void AttachViewModelHandlers(ErrorAnalysisViewModel? viewModel)
    {
        if (!IsLoaded || viewModel == null || ReferenceEquals(_attachedViewModel, viewModel))
        {
            return;
        }

        _attachedViewModel = viewModel;
        _attachedViewModel.ChatMessages.CollectionChanged += ChatMessages_CollectionChanged;
        _attachedViewModel.PropertyChanged += ViewModel_PropertyChanged;

        if (_attachedViewModel.ChatMessages.LastOrDefault() is UiChatMessage lastMessage)
        {
            _lastChatMessage = lastMessage;
            _lastChatMessage.PropertyChanged += ChatMessage_PropertyChanged;
        }

        UpdateMessageRolePresentation();
    }

    private void DetachViewModelHandlers(ErrorAnalysisViewModel? viewModel)
    {
        if (!ReferenceEquals(_attachedViewModel, viewModel) || _attachedViewModel == null)
        {
            return;
        }

        _attachedViewModel.ChatMessages.CollectionChanged -= ChatMessages_CollectionChanged;
        _attachedViewModel.PropertyChanged -= ViewModel_PropertyChanged;

        if (_lastChatMessage != null)
        {
            _lastChatMessage.PropertyChanged -= ChatMessage_PropertyChanged;
            _lastChatMessage = null;
        }

        _attachedViewModel = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ErrorAnalysisViewModel.HasChatMessages))
        {
            UpdatePlaceholderState();
        }

        if (e.PropertyName == nameof(ErrorAnalysisViewModel.CanComposeChat)
            || e.PropertyName == nameof(ErrorAnalysisViewModel.IsAiAnalyzing))
        {
            UpdateComposerState();
        }
    }

    private void UpdateComposerState()
    {
        bool isEnabled = IsComposerEnabled && ViewModel?.CanComposeChat == true;

        ChatInputBox.IsEnabled = isEnabled;

        if (AddImageButton != null)
        {
            AddImageButton.IsEnabled = isEnabled;
        }

        if (ChatActionButton != null)
        {
            ChatActionButton.IsEnabled = isEnabled;
        }
    }

    private void OnChatListViewLoaded(object sender, RoutedEventArgs e)
    {
        _chatScrollViewer = FindScrollViewer(ChatListView);
        if (_chatScrollViewer != null)
        {
            _chatScrollViewer.ViewChanged -= ChatScrollViewer_ViewChanged;
            _chatScrollViewer.ViewChanged += ChatScrollViewer_ViewChanged;
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv)
            {
                return sv;
            }

            var result = FindScrollViewer(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void ChatScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_chatScrollViewer == null || e.IsIntermediate)
        {
            return;
        }

        var distanceFromBottom = _chatScrollViewer.ScrollableHeight - _chatScrollViewer.VerticalOffset;
        _userIsScrollingChat = distanceFromBottom > 20;
    }

    private void ChatMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateMessageRolePresentation();

        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<UiChatMessage>())
                {
                    if (_lastChatMessage != null)
                    {
                        _lastChatMessage.PropertyChanged -= ChatMessage_PropertyChanged;
                    }

                    _lastChatMessage = item;
                    _lastChatMessage.PropertyChanged += ChatMessage_PropertyChanged;
                }
            }

            _userIsScrollingChat = false;
            UpdatePlaceholderState();
            _ = ScrollChatToBottomAsync();
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _userIsScrollingChat = false;

            if (_lastChatMessage != null)
            {
                _lastChatMessage.PropertyChanged -= ChatMessage_PropertyChanged;
                _lastChatMessage = null;
            }

            UpdatePlaceholderState();
        }
        else
        {
            UpdatePlaceholderState();
        }
    }

    private void ChatMessage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UiChatMessage.Content) || _userIsScrollingChat || _isChatScrollPending)
        {
            return;
        }

        _isChatScrollPending = true;
        _ = ScrollChatToBottomAsync();
    }

    private async Task ScrollChatToBottomAsync()
    {
        try
        {
            await Task.Delay(50);
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                if (_chatScrollViewer != null)
                {
                    _chatScrollViewer.ChangeView(null, _chatScrollViewer.ScrollableHeight, null, true);
                }
                else if (ChatListView.Items.Count > 0)
                {
                    ChatListView.ScrollIntoView(ChatListView.Items[ChatListView.Items.Count - 1]);
                }
            });
        }
        catch
        {
        }
        finally
        {
            _isChatScrollPending = false;
        }
    }

    private void UpdatePlaceholderState()
    {
        var shouldShow = ShowEmptyPlaceholder && (ViewModel?.HasChatMessages != true);
        EmptyStatePanel.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateMessageRolePresentation()
    {
        if (_attachedViewModel == null)
        {
            return;
        }

        string? previousNonToolRole = null;
        foreach (var message in _attachedViewModel.ChatMessages)
        {
            if (message.IsTool)
            {
                message.DisplayRoleText = string.Empty;
                message.ShowRoleHeader = false;
                continue;
            }

            message.DisplayRoleText = message.Role;
            message.ShowRoleHeader = !string.Equals(previousNonToolRole, message.Role, StringComparison.OrdinalIgnoreCase);
            previousNonToolRole = message.Role;
        }
    }

    private void ChatInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || ViewModel == null)
        {
            return;
        }

        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        if (!shift.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) && ViewModel.SendMessageCommand.CanExecute(null))
        {
            ViewModel.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ChatImageCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel == null
            || sender is not FrameworkElement element
            || element.Tag is not ChatImageAttachment attachment)
        {
            return;
        }

        if (ViewModel.OpenChatAttachmentCommand.CanExecute(attachment))
        {
            ViewModel.OpenChatAttachmentCommand.Execute(attachment);
        }
    }

    private void ChatImageCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        SetCardOpacity(sender, 0.9);
    }

    private void ChatImageCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        SetCardOpacity(sender, 1.0);
    }

    private void ChatImageCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        SetCardOpacity(sender, 0.8);
    }

    private void ChatImageCard_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        SetCardOpacity(sender, 0.9);
    }

    private static void SetCardOpacity(object sender, double opacity)
    {
        if (sender is UIElement element)
        {
            element.Opacity = opacity;
        }
    }
}
