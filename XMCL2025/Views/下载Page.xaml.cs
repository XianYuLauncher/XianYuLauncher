using Microsoft.UI.Xaml.Controls;

using XMCL2025.Core.Contracts.Services;
using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class 下载Page : Page
{
    public 下载ViewModel ViewModel
    {
        get;
    }

    public 下载Page()
    {
        ViewModel = App.GetService<下载ViewModel>();
        InitializeComponent();
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // 搜索逻辑已经在ViewModel中通过绑定实现
    }

    private void VersionsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is VersionEntry versionEntry)
        {
            ViewModel.DownloadVersionCommand.Execute(versionEntry.Id);
        }
    }
}
