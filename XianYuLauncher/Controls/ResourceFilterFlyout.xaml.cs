using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Labs.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace XianYuLauncher.Controls;

public sealed partial class ResourceFilterFlyout : Microsoft.UI.Xaml.Controls.UserControl
{
    private bool _isUpdatingSelection = false;
    private bool _isUpdatingShowAllVersions = false;
        private string? _lastLoadersSignature;
        private string? _lastCategoriesSignature;
        private string? _lastVersionsSignature;

    #region Dependency Properties - Data Sources

    public static readonly DependencyProperty LoadersSourceProperty =
        DependencyProperty.Register(
            nameof(LoadersSource),
            typeof(ObservableCollection<TokenItem>),
            typeof(ResourceFilterFlyout),
            new PropertyMetadata(null, OnLoadersSourceChanged));

    public ObservableCollection<TokenItem>? LoadersSource
    {
        get => (ObservableCollection<TokenItem>?)GetValue(LoadersSourceProperty);
        set => SetValue(LoadersSourceProperty, value);
    }

    public static readonly DependencyProperty CategoriesSourceProperty =
        DependencyProperty.Register(
            nameof(CategoriesSource),
            typeof(ObservableCollection<TokenItem>),
            typeof(ResourceFilterFlyout),
            new PropertyMetadata(null, OnCategoriesSourceChanged));

    public ObservableCollection<TokenItem>? CategoriesSource
    {
        get => (ObservableCollection<TokenItem>?)GetValue(CategoriesSourceProperty);
        set => SetValue(CategoriesSourceProperty, value);
    }

    public static readonly DependencyProperty VersionsSourceProperty =
        DependencyProperty.Register(
            nameof(VersionsSource),
            typeof(ObservableCollection<TokenItem>),
            typeof(ResourceFilterFlyout),
            new PropertyMetadata(null, OnVersionsSourceChanged));

    public ObservableCollection<TokenItem>? VersionsSource
    {
        get => (ObservableCollection<TokenItem>?)GetValue(VersionsSourceProperty);
        set => SetValue(VersionsSourceProperty, value);
    }

    public static readonly DependencyProperty IsShowAllVersionsProperty =
        DependencyProperty.Register(
            nameof(IsShowAllVersions),
            typeof(bool),
            typeof(ResourceFilterFlyout),
            new PropertyMetadata(false));

    public bool IsShowAllVersions
    {
        get => (bool)GetValue(IsShowAllVersionsProperty);
        set => SetValue(IsShowAllVersionsProperty, value);
    }

    #endregion

    #region Read-only Selected Tags

    public IList<string> SelectedLoaderTags => GetSelectedTags(LoaderTokenView);

    public IList<string> SelectedCategoryTags => GetSelectedTags(CategoryTokenView);

    public IList<string> SelectedVersionTags => GetSelectedTags(VersionTokenView);

    #endregion

    #region Events

    public event EventHandler? SelectionChanged;

    public event EventHandler? ShowAllVersionsChanged;

    /// <summary>
    /// 当需要刷新版本列表时触发（CheckBox 点击时）
    /// </summary>
    public event EventHandler? RefreshVersionsRequested;

    #endregion

    public ResourceFilterFlyout()
    {
        this.InitializeComponent();
    }

    private static void OnLoadersSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResourceFilterFlyout control && e.NewValue is ObservableCollection<TokenItem> loaders)
        {
            control.RefreshTokenItems(control.LoaderTokenView, loaders);
        }
    }

    private static void OnCategoriesSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResourceFilterFlyout control && e.NewValue is ObservableCollection<TokenItem> categories)
        {
            control.RefreshTokenItems(control.CategoryTokenView, categories);
        }
    }

    private static void OnVersionsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResourceFilterFlyout control && e.NewValue is ObservableCollection<TokenItem> versions)
        {
            control.RefreshTokenItems(control.VersionTokenView, versions);
        }
    }

    private void RefreshTokenItems(TokenView tokenView, ObservableCollection<TokenItem> items)
    {
        if (tokenView == null) return;

        // 先计算当前 items 的签名（基于 Tag 与 Content），用于检测是否需要真正重建
        string MakeSignature(ObservableCollection<TokenItem> src)
        {
            return string.Join("|", src.Select(i => (i.Tag?.ToString() ?? "") + ":" + (i.Content?.ToString() ?? "")));
        }

        string signature = MakeSignature(items);

        // 如果签名与上次相同且已有 Items，则不重新渲染，避免触发不必要的动画
        if (tokenView == LoaderTokenView && _lastLoadersSignature == signature && tokenView.Items.Count > 0)
            return;
        if (tokenView == CategoryTokenView && _lastCategoriesSignature == signature && tokenView.Items.Count > 0)
            return;
        if (tokenView == VersionTokenView && _lastVersionsSignature == signature && tokenView.Items.Count > 0)
            return;

        // 克隆传入的 TokenItem 对象，避免直接复用引用导致 TokenView 对比失败
        _isUpdatingSelection = true;
        try
        {
            // 记录当前选中的 tag（以便在重建后恢复选中）
            var prevSelectedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TokenItem t in tokenView.SelectedItems.OfType<TokenItem>())
            {
                var tg = t.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tg)) prevSelectedTags.Add(tg);
            }

            SafeClearItems(tokenView.Items);

            foreach (var src in items)
            {
                var clone = new TokenItem
                {
                    Content = src.Content,
                    Tag = src.Tag,
                    Margin = src.Margin,
                    Padding = src.Padding,
                };

                // 复制 Icon（若为 FontIcon，则拷贝 Glyph）
                try
                {
                    if (src.Icon is FontIcon fIcon)
                    {
                        clone.Icon = new FontIcon { Glyph = fIcon.Glyph };
                    }
                    else
                    {
                        clone.Icon = src.Icon;
                    }
                }
                catch
                {
                    // 忽略 Icon 复制失败，保持最小信息（Content/Tag）
                }

                tokenView.Items.Add(clone);

                // 恢复先前选中状态（如果存在）
                var tagStr = clone.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tagStr) && prevSelectedTags.Contains(tagStr))
                {
                    tokenView.SelectedItems.Add(clone);
                }
            }
            // 更新签名缓存
            if (tokenView == LoaderTokenView) _lastLoadersSignature = signature;
            if (tokenView == CategoryTokenView) _lastCategoriesSignature = signature;
            if (tokenView == VersionTokenView) _lastVersionsSignature = signature;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void TokenView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        if (sender is TokenView tokenView)
        {
            // 复用通用的多选逻辑（互斥处理）
            HandleMultiSelection(tokenView, e);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // 通用的多选互斥处理逻辑（All 与 具体项互斥）
    private void HandleMultiSelection(
        TokenView tokenView,
        SelectionChangedEventArgs e)
    {
        var selectedTokenTags = tokenView.SelectedItems
            .OfType<TokenItem>()
            .Select(item => item.Tag?.ToString())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToList();

        var allSelected = selectedTokenTags.Any(tag => string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase));
        var allToken = tokenView.Items
            .OfType<TokenItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), "all", StringComparison.OrdinalIgnoreCase));
        var allAddedThisTime = e.AddedItems
            .OfType<TokenItem>()
            .Any(item => string.Equals(item.Tag?.ToString(), "all", StringComparison.OrdinalIgnoreCase));
        var selectedNonAllTags = selectedTokenTags
            .Where(tag => !string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _isUpdatingSelection = true;
        try
        {
            if (allAddedThisTime)
            {
                // 用户显式点了"所有"，强制只保留 all。
                SafeClearSelection(tokenView.SelectedItems);
                if (allToken != null)
                {
                    tokenView.SelectedItems.Add(allToken);
                }
            }
            else if (allSelected && selectedNonAllTags.Count > 0 && allToken != null)
            {
                // 选中具体类别时自动移除 all。
                tokenView.SelectedItems.Remove(allToken);
            }
            else if (!allSelected && selectedNonAllTags.Count == 0 && allToken != null)
            {
                // 所有具体项都被取消后，回退到 all。
                tokenView.SelectedItems.Add(allToken);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private static IList<string> GetSelectedTags(TokenView tokenView)
    {
        if (tokenView == null) return new List<string>();

        var selectedTokenTags = tokenView.SelectedItems
            .OfType<TokenItem>()
            .Select(item => item.Tag?.ToString())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToList();

        return selectedTokenTags
            .Where(tag => !string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ShowAllVersionsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingShowAllVersions) return;

        _isUpdatingShowAllVersions = true;
        try
        {
            // Checked/Unchecked 事件触发时，IsChecked 已经是新状态
            IsShowAllVersions = ShowAllVersionsCheckBox.IsChecked == true;

            // 通知外部 IsShowAllVersions 已变化（外部会更新 ViewModel、异步刷新版本列表等）
            ShowAllVersionsChanged?.Invoke(this, EventArgs.Empty);
            // 注意：不再在此处直接触发 RefreshVersionsRequested，避免与外部异步刷新 AvailableVersions 产生竞态；
            // 外部应在完成版本列表刷新后，再在合适时机刷新 TokenView 或调用相应刷新逻辑。
        }
        finally
        {
            _isUpdatingShowAllVersions = false;
        }
    }

    private static void SafeClearItems(ItemCollection items)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            items.RemoveAt(i);
        }
    }

    private static void SafeClearSelection(IList<object> selectedItems)
    {
        for (int i = selectedItems.Count - 1; i >= 0; i--)
        {
            selectedItems.RemoveAt(i);
        }
    }

    /// <summary>
    /// 设置选中的加载器标签（用于外部初始化）
    /// </summary>
    public void SetSelectedLoaders(IEnumerable<string> tags)
    {
        SetSelectedItems(LoaderTokenView, tags);
    }

    /// <summary>
    /// 设置选中的类别标签（用于外部初始化）
    /// </summary>
    public void SetSelectedCategories(IEnumerable<string> tags)
    {
        SetSelectedItems(CategoryTokenView, tags);
    }

    /// <summary>
    /// 设置选中的版本标签（用于外部初始化）
    /// </summary>
    public void SetSelectedVersions(IEnumerable<string> tags)
    {
        SetSelectedItems(VersionTokenView, tags);
    }

    private void SetSelectedItems(TokenView tokenView, IEnumerable<string> tags)
    {
        if (tokenView == null) return;

        _isUpdatingSelection = true;
        try
        {
            SafeClearSelection(tokenView.SelectedItems);

            var tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);

            foreach (TokenItem item in tokenView.Items)
            {
                var itemTag = item.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(itemTag) && tagSet.Contains(itemTag))
                {
                    tokenView.SelectedItems.Add(item);
                }
            }

            // 如果没有任何选中项，默认回退到 "all"（保持与旧逻辑一致）
            if (tokenView.SelectedItems.Count == 0)
            {
                var allToken = tokenView.Items
                    .OfType<TokenItem>()
                    .FirstOrDefault(it => string.Equals(it.Tag?.ToString(), "all", StringComparison.OrdinalIgnoreCase));
                if (allToken != null)
                {
                    tokenView.SelectedItems.Add(allToken);
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }
}
