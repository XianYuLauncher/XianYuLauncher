using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class WorldManagementPage : Page
{
    public WorldManagementViewModel ViewModel { get; }

    public WorldManagementPage()
    {
        ViewModel = App.GetService<WorldManagementViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is string worldPath)
        {
            await ViewModel.InitializeAsync(worldPath);
        }
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            // TODO: 根据 tag 切换不同的内容
            switch (tag)
            {
                case "overview":
                    // 显示概览
                    break;
                case "datapacks":
                    // 显示数据包管理
                    break;
                case "settings":
                    // 显示设置
                    break;
            }
        }
    }
}
