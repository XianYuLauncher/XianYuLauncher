using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.ResourceDownload.ViewModels;

namespace XianYuLauncher.Features.ResourceDownload.Views;

public sealed partial class ResourceDownloadRootPage : Page
{
    public ResourceDownloadViewModel ViewModel { get; }

    public ResourceDownloadRootPage()
    {
        ViewModel = App.GetService<ResourceDownloadViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void AttachContent(UIElement content)
    {
        if (ReferenceEquals(RootContentHost.Content, content))
        {
            return;
        }

        RootContentHost.Content = content;
    }

    public void DetachContent()
    {
        RootContentHost.Content = null;
    }
}