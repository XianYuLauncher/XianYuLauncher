using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.Features.ModLoaderSelector.ViewModels;

namespace XianYuLauncher.Features.ModLoaderSelector.Views;

public sealed partial class ModLoaderSelectorContentPage : Page
{
    public ModLoaderSelectorViewModel ViewModel { get; }

    public ModLoaderSelectorContentPage()
    {
        ViewModel = App.GetService<ModLoaderSelectorViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e.Parameter!);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }
}