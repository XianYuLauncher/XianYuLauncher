using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Services;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class NewsListPage : Page
{
    public NewsListViewModel ViewModel { get; } = new NewsListViewModel();

    public NewsListPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadNewsAsync();
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

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterComboBox.SelectedItem is ComboBoxItem item)
        {
            // 使用 Tag 而不是 Content，避免本地化问题
            ViewModel.SelectedFilter = item.Tag?.ToString() ?? "All";
        }
    }

    private void NewsItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MinecraftNewsEntry entry)
        {
            ViewModel.OpenNewsDetailCommand.Execute(entry);
        }
    }
}
