using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Threading.Tasks;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Services;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class NewsListPage : Page
{
    private const string SourceAll = "All";
    private const string SourceJavaPatchNote = "JavaPatchNote";
    private const string SourceNews = "News";
    private bool _isInitializingFilters;

    public NewsListViewModel ViewModel { get; } = new NewsListViewModel();

    public NewsListPage()
    {
        _isInitializingFilters = true;
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateDetailFilterOptions(SourceAll);
        _isInitializingFilters = false;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ApplyCurrentFiltersAsync();
        UpdateUI();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        // 更新加载状态
        LoadingPanel.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        
        // 更新错误信息
        if (!string.IsNullOrEmpty(ViewModel.ErrorMessage) && !ViewModel.IsLoading)
        {
            ErrorTextBlock.Text = ViewModel.ErrorMessage;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }
        
        // 更新列表
        if (!ViewModel.IsLoading && ViewModel.NewsItems.Count > 0)
        {
            NewsListView.ItemsSource = ViewModel.NewsItems;
            NewsListView.Visibility = Visibility.Visible;
        }
        else if (!ViewModel.IsLoading)
        {
            NewsListView.Visibility = Visibility.Collapsed;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        if (navigationService.CanGoBack)
        {
            navigationService.GoBack();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void SourceFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingFilters || FilterComboBox == null)
        {
            return;
        }
        var selectedSource = GetSelectedTag(SourceFilterComboBox, SourceAll);
        UpdateDetailFilterOptions(selectedSource);
        await ApplyCurrentFiltersAsync();
    }

    private async void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingFilters)
        {
            return;
        }
        await ApplyCurrentFiltersAsync();
    }

    private void NewsItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MinecraftNewsEntry entry)
        {
            if (sender is ListView listView)
            {
                ActivityNewsTeachingTip.Target = listView.ContainerFromItem(entry) as FrameworkElement ?? NewsListView;
            }
            ViewModel.OpenNewsDetailCommand.Execute(entry);
        }
    }

    private void ActivityNewsTeachingTip_CloseButtonClick(TeachingTip sender, object args)
    {
        ViewModel.CloseNewsTeachingTipCommand.Execute(null);
    }

    private async Task ApplyCurrentFiltersAsync()
    {
        if (SourceFilterComboBox == null || FilterComboBox == null)
        {
            return;
        }
        var sourceFilter = GetSelectedTag(SourceFilterComboBox, SourceAll);
        var detailFilter = GetSelectedTag(
            FilterComboBox,
            sourceFilter switch
            {
                SourceNews => "All",
                SourceJavaPatchNote => "All",
                _ => "All"
            });
        await ViewModel.ApplyFiltersAsync(sourceFilter, detailFilter);
    }

    private void UpdateDetailFilterOptions(string sourceFilter)
    {
        if (FilterComboBox == null)
        {
            return;
        }

        var options = sourceFilter switch
        {
            SourceNews => new List<(string Label, string Tag)>
            {
                ("全部", "All"),
                ("Minecraft for Windows", "Windows"),
                ("Minecraft: Java Edition", "JavaEdition")
            },
            SourceJavaPatchNote => new List<(string Label, string Tag)>
            {
                ("全部", "All"),
                ("正式版", "Release"),
                ("快照", "Snapshot")
            },
            _ => new List<(string Label, string Tag)>
            {
                ("全部", "All")
            }
        };

        FilterComboBox.Items.Clear();
        foreach (var (label, tag) in options)
        {
            FilterComboBox.Items.Add(new ComboBoxItem
            {
                Content = label,
                Tag = tag
            });
        }
        FilterComboBox.SelectedIndex = 0;
        FilterComboBox.IsEnabled = sourceFilter != SourceAll;
    }

    private static string GetSelectedTag(ComboBox comboBox, string defaultValue)
    {
        if (comboBox.SelectedItem is ComboBoxItem item)
        {
            return item.Tag?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }
}
