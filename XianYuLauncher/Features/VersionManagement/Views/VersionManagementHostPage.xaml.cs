using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.VersionManagement.Models;
using XianYuLauncher.Features.VersionManagement.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.Views;

public sealed partial class VersionManagementHostPage : Page, IHostedLocalPage
{
    private EventHandler? _closeRequested;
    private bool _usesPageLevelNavigationForwarding;

    public VersionManagementViewModel ViewModel { get; }

    public IPageHeaderAware HeaderSource => ViewModel;

    public event EventHandler? CloseRequested
    {
        add => _closeRequested += value;
        remove => _closeRequested -= value;
    }

    public VersionManagementHostPage()
    {
        ViewModel = App.GetService<VersionManagementViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _usesPageLevelNavigationForwarding = e.Parameter is VersionManagementNavigationParameter;
        if (_usesPageLevelNavigationForwarding)
        {
            ViewModel.OnNavigatedTo(e.Parameter);
        }

        if (e.NavigationMode == NavigationMode.Back && VersionManagementInnerContentFrame.Content is not null)
        {
            return;
        }

        NavigateToRootContent(suppressTransition: true);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        if (_usesPageLevelNavigationForwarding)
        {
            ViewModel.OnNavigatedFrom();
            _usesPageLevelNavigationForwarding = false;
        }
    }

    public void ResetEmbeddedVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        VersionManagementInnerContentFrame.Opacity = 1;
        VersionManagementInnerContentFrame.Translation = default;

        if (VersionManagementInnerContentFrame.Content is VersionManagementPage rootPage)
        {
            rootPage.ResetEmbeddedVisualState();
        }
    }

    private void NavigateToRootContent(bool suppressTransition)
    {
        NavigationTransitionInfo transition = suppressTransition
            ? new SuppressNavigationTransitionInfo()
            : new EntranceNavigationTransitionInfo();

        VersionManagementInnerContentFrame.Navigate(typeof(VersionManagementPage), ViewModel, transition);
        VersionManagementInnerContentFrame.BackStack.Clear();
        VersionManagementInnerContentFrame.ForwardStack.Clear();
    }
}
