using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Controls;

public sealed partial class LauncherAiWorkspacePanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(LauncherAiViewModel),
        typeof(LauncherAiWorkspacePanel),
        new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty EmptyPlaceholderTextProperty = DependencyProperty.Register(
        nameof(EmptyPlaceholderText),
        typeof(string),
        typeof(LauncherAiWorkspacePanel),
        new PropertyMetadata(string.Empty, OnPresentationPropertyChanged));

    public static readonly DependencyProperty MessagesMaxHeightProperty = DependencyProperty.Register(
        nameof(MessagesMaxHeight),
        typeof(double),
        typeof(LauncherAiWorkspacePanel),
        new PropertyMetadata(double.PositiveInfinity, OnPresentationPropertyChanged));

    private bool _isRefreshingTabs;
    private readonly Dictionary<Guid, TabViewItem> _tabItems = [];

    public LauncherAiWorkspacePanel()
    {
        InitializeComponent();
        Unloaded += LauncherAiWorkspacePanel_Unloaded;
    }

    public LauncherAiViewModel? ViewModel
    {
        get => (LauncherAiViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public ErrorAnalysisViewModel? ChatViewModel => ViewModel?.ChatViewModel;

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

    public string ResolvedEmptyStateText => string.IsNullOrWhiteSpace(EmptyPlaceholderText)
        ? ViewModel?.EmptyStateText ?? string.Empty
        : EmptyPlaceholderText;

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LauncherAiWorkspacePanel panel)
        {
            return;
        }

        panel.DetachViewModelHandlers(e.OldValue as LauncherAiViewModel);
        panel.AttachViewModelHandlers(e.NewValue as LauncherAiViewModel);
        panel.UpdateChatPanel();
        panel.RebuildTabs();
    }

    private static void OnPresentationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LauncherAiWorkspacePanel panel)
        {
            panel.UpdateChatPanel();
        }
    }

    private void AttachViewModelHandlers(LauncherAiViewModel? viewModel)
    {
        if (viewModel == null)
        {
            return;
        }

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.Conversations.CollectionChanged += Conversations_CollectionChanged;

        foreach (var conversation in viewModel.Conversations)
        {
            conversation.PropertyChanged += Conversation_PropertyChanged;
        }
    }

    private void DetachViewModelHandlers(LauncherAiViewModel? viewModel)
    {
        if (viewModel == null)
        {
            return;
        }

        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        viewModel.Conversations.CollectionChanged -= Conversations_CollectionChanged;

        foreach (var conversation in viewModel.Conversations)
        {
            conversation.PropertyChanged -= Conversation_PropertyChanged;
        }

        _tabItems.Clear();
        ConversationTabView?.TabItems.Clear();
    }

    private void LauncherAiWorkspacePanel_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModelHandlers(ViewModel);
    }

    private void Conversations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (LauncherAiConversationTab conversation in e.OldItems)
            {
                conversation.PropertyChanged -= Conversation_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (LauncherAiConversationTab conversation in e.NewItems)
            {
                conversation.PropertyChanged += Conversation_PropertyChanged;
            }
        }

        RebuildTabs();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            UpdateChatPanel();
            UpdateSelection();
            return;
        }

        if (e.PropertyName == nameof(LauncherAiViewModel.SelectedConversation))
        {
            UpdateSelection();
        }
        else if (e.PropertyName == nameof(LauncherAiViewModel.EmptyStateText))
        {
            UpdateChatPanel();
        }
    }

    private void Conversation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LauncherAiConversationTab conversation
            || !_tabItems.TryGetValue(conversation.Id, out var item))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || e.PropertyName == nameof(LauncherAiConversationTab.Title))
        {
            item.Header = conversation.Title;
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || e.PropertyName == nameof(LauncherAiConversationTab.ToolTip))
        {
            ToolTipService.SetToolTip(item, conversation.ToolTip);
        }
    }

    private void ConversationTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTabs || ViewModel == null)
        {
            return;
        }

        if (ConversationTabView.SelectedItem is TabViewItem item && item.Tag is Guid conversationId)
        {
            if (!ViewModel.TrySelectConversation(conversationId))
            {
                UpdateSelection();
            }
        }
    }

    private void ConversationTabView_AddTabButtonClick(TabView sender, object args)
    {
        ViewModel?.CreateConversation();
    }

    private void ConversationTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (ViewModel == null || args.Tab is not TabViewItem item || item.Tag is not Guid conversationId)
        {
            return;
        }

        ViewModel.CloseConversation(conversationId);
    }

    private void RebuildTabs()
    {
        if (ConversationTabView == null)
        {
            return;
        }

        _isRefreshingTabs = true;
        try
        {
            ConversationTabView.TabItems.Clear();
            _tabItems.Clear();

            if (ViewModel == null)
            {
                return;
            }

            var canClose = ViewModel.Conversations.Count > 1;
            foreach (var conversation in ViewModel.Conversations)
            {
                var item = new TabViewItem
                {
                    Header = conversation.Title,
                    Tag = conversation.Id,
                    IsClosable = canClose,
                    Content = null,
                };

                ToolTipService.SetToolTip(item, conversation.ToolTip);
                _tabItems[conversation.Id] = item;
                ConversationTabView.TabItems.Add(item);
            }

            UpdateSelection();
        }
        finally
        {
            _isRefreshingTabs = false;
        }
    }

    private void UpdateSelection()
    {
        if (ViewModel?.SelectedConversation?.Id is Guid selectedId
            && _tabItems.TryGetValue(selectedId, out var item))
        {
            ConversationTabView.SelectedItem = item;
        }
        else if (ConversationTabView.SelectedItem != null)
        {
            ConversationTabView.SelectedItem = null;
        }
    }

    private void UpdateChatPanel()
    {
        if (ChatPanel == null)
        {
            return;
        }

        ChatPanel.ViewModel = ChatViewModel;
        ChatPanel.EmptyPlaceholderText = ResolvedEmptyStateText;
        ChatPanel.MessagesMaxHeight = MessagesMaxHeight;
    }
}