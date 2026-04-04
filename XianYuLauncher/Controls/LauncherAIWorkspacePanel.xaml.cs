using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.ErrorAnalysis.ViewModels;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Controls;

public sealed partial class LauncherAIWorkspacePanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(LauncherAIViewModel),
        typeof(LauncherAIWorkspacePanel),
        new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty EmptyPlaceholderTextProperty = DependencyProperty.Register(
        nameof(EmptyPlaceholderText),
        typeof(string),
        typeof(LauncherAIWorkspacePanel),
        new PropertyMetadata(string.Empty, OnPresentationPropertyChanged));

    public static readonly DependencyProperty MessagesMaxHeightProperty = DependencyProperty.Register(
        nameof(MessagesMaxHeight),
        typeof(double),
        typeof(LauncherAIWorkspacePanel),
        new PropertyMetadata(double.PositiveInfinity, OnPresentationPropertyChanged));

    private bool _isRefreshingTabs;
    private readonly Dictionary<Guid, TabViewItem> _tabItems = [];

    public LauncherAIWorkspacePanel()
    {
        InitializeComponent();
        Unloaded += LauncherAIWorkspacePanel_Unloaded;
    }

    public LauncherAIViewModel? ViewModel
    {
        get => (LauncherAIViewModel?)GetValue(ViewModelProperty);
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
        if (d is not LauncherAIWorkspacePanel panel)
        {
            return;
        }

        panel.DetachViewModelHandlers(e.OldValue as LauncherAIViewModel);
        panel.AttachViewModelHandlers(e.NewValue as LauncherAIViewModel);
        panel.UpdateChatPanel();
        panel.RebuildTabs();
    }

    private static void OnPresentationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LauncherAIWorkspacePanel panel)
        {
            panel.UpdateChatPanel();
        }
    }

    private void AttachViewModelHandlers(LauncherAIViewModel? viewModel)
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

    private void DetachViewModelHandlers(LauncherAIViewModel? viewModel)
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

    private void LauncherAIWorkspacePanel_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModelHandlers(ViewModel);
    }

    private void Conversations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (LauncherAIConversationTab conversation in e.OldItems)
            {
                conversation.PropertyChanged -= Conversation_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (LauncherAIConversationTab conversation in e.NewItems)
            {
                conversation.PropertyChanged += Conversation_PropertyChanged;
            }
        }

        SyncTabs(e);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            UpdateChatPanel();
            UpdateSelection();
            return;
        }

        if (e.PropertyName == nameof(LauncherAIViewModel.SelectedConversation))
        {
            UpdateSelection();
        }
        else if (e.PropertyName == nameof(LauncherAIViewModel.EmptyStateText))
        {
            UpdateChatPanel();
        }
    }

    private void Conversation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LauncherAIConversationTab conversation
            || !_tabItems.TryGetValue(conversation.Id, out var item))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || e.PropertyName == nameof(LauncherAIConversationTab.Title))
        {
            item.Header = conversation.Title;
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || e.PropertyName == nameof(LauncherAIConversationTab.ToolTip))
        {
            ToolTipService.SetToolTip(item, conversation.ToolTip);
        }
    }

    private void ConversationTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var conversationTabView = ConversationTabView;

        if (_isRefreshingTabs || ViewModel == null || conversationTabView == null)
        {
            return;
        }

        if (conversationTabView.SelectedItem is TabViewItem item
            && TryResolveConversationId(item.DataContext, item, out var conversationId))
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
        if (ViewModel == null
            || !TryResolveConversationId(args.Item, args.Tab as TabViewItem, out var conversationId))
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

            foreach (var conversation in ViewModel.Conversations)
            {
                AddTabItem(conversation, ConversationTabView.TabItems.Count);
            }

            UpdateClosableState();
            UpdateSelection();
        }
        finally
        {
            _isRefreshingTabs = false;
        }
    }

    private void UpdateSelection()
    {
        var conversationTabView = ConversationTabView;

        if (conversationTabView == null)
        {
            return;
        }

        if (ViewModel?.SelectedConversation?.Id is Guid selectedId
            && _tabItems.TryGetValue(selectedId, out var item))
        {
            conversationTabView.SelectedItem = item;
        }
        else if (conversationTabView.SelectedItem != null)
        {
            conversationTabView.SelectedItem = null;
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

    private void SyncTabs(NotifyCollectionChangedEventArgs e)
    {
        if (ConversationTabView == null || ViewModel == null)
        {
            return;
        }

        _isRefreshingTabs = true;
        try
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertNewTabs(e.NewItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveTabs(e.OldItems);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    RemoveTabs(e.OldItems);
                    InsertNewTabs(e.NewItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    MoveTabs(e.OldItems, e.NewStartingIndex);
                    break;
                default:
                    RebuildTabs();
                    return;
            }

            UpdateClosableState();
            UpdateSelection();
        }
        finally
        {
            _isRefreshingTabs = false;
        }
    }

    private void InsertNewTabs(System.Collections.IList? newItems, int startIndex)
    {
        if (newItems == null)
        {
            return;
        }

        var insertIndex = startIndex < 0 ? ConversationTabView.TabItems.Count : startIndex;
        for (var offset = 0; offset < newItems.Count; offset++)
        {
            if (newItems[offset] is LauncherAIConversationTab conversation)
            {
                AddTabItem(conversation, Math.Min(insertIndex + offset, ConversationTabView.TabItems.Count));
            }
        }
    }

    private void RemoveTabs(System.Collections.IList? oldItems)
    {
        if (oldItems == null)
        {
            return;
        }

        foreach (var item in oldItems)
        {
            if (item is LauncherAIConversationTab conversation)
            {
                RemoveTabItem(conversation.Id);
            }
        }
    }

    private void MoveTabs(System.Collections.IList? movedItems, int newStartingIndex)
    {
        if (movedItems == null)
        {
            return;
        }

        var targetIndex = newStartingIndex;
        foreach (var item in movedItems)
        {
            if (item is not LauncherAIConversationTab conversation
                || !_tabItems.TryGetValue(conversation.Id, out var tabItem))
            {
                continue;
            }

            ConversationTabView.TabItems.Remove(tabItem);
            ConversationTabView.TabItems.Insert(Math.Min(targetIndex, ConversationTabView.TabItems.Count), tabItem);
            targetIndex++;
        }
    }

    private void AddTabItem(LauncherAIConversationTab conversation, int insertIndex)
    {
        if (ConversationTabView == null || _tabItems.ContainsKey(conversation.Id))
        {
            return;
        }

        var item = new TabViewItem
        {
            Header = conversation.Title,
            DataContext = conversation,
            Tag = conversation.Id,
            Content = null,
        };

        ToolTipService.SetToolTip(item, conversation.ToolTip);
        _tabItems[conversation.Id] = item;
        ConversationTabView.TabItems.Insert(insertIndex, item);
    }

    private static bool TryResolveConversationId(object? itemData, TabViewItem? tabItem, out Guid conversationId)
    {
        if (itemData is LauncherAIConversationTab conversation)
        {
            conversationId = conversation.Id;
            return true;
        }

        if (tabItem?.DataContext is LauncherAIConversationTab tabConversation)
        {
            conversationId = tabConversation.Id;
            return true;
        }

        if (tabItem?.Tag is Guid tagConversationId)
        {
            conversationId = tagConversationId;
            return true;
        }

        conversationId = Guid.Empty;
        return false;
    }

    private void RemoveTabItem(Guid conversationId)
    {
        if (ConversationTabView == null || !_tabItems.Remove(conversationId, out var item))
        {
            return;
        }

        ConversationTabView.TabItems.Remove(item);
    }

    private void UpdateClosableState()
    {
        var canClose = ViewModel != null && ViewModel.Conversations.Count > 1;
        foreach (var item in _tabItems.Values)
        {
            item.IsClosable = canClose;
        }
    }
}