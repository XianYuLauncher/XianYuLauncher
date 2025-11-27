using Microsoft.UI.Xaml.Controls;
using XMCL2025.Contracts.Services;
using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class 版本列表Page : Page
{
    private readonly INavigationService _navigationService;

    public 版本列表Page()
    {
        this.DataContext = App.GetService<版本列表ViewModel>();
        _navigationService = App.GetService<INavigationService>();
        InitializeComponent();
        
        // 添加ItemClick事件处理
        VersionsListView.ItemClick += VersionsListView_ItemClick;
    }

    /// <summary>
    /// 版本项点击事件处理，导航至版本管理页面
    /// </summary>
    private void VersionsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is 版本列表ViewModel.VersionInfoItem version)
        {
            // 导航至版本管理页面，传递选中的版本信息
            _navigationService.NavigateTo(typeof(版本管理ViewModel).FullName!, version);
        }
    }
}