using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.UI.Xaml.Controls;

using XMCL2025.Contracts.Services;
using XMCL2025.ViewModels;
using XMCL2025.Views;

namespace XMCL2025.Services;

public class PageService : IPageService
{
    private readonly Dictionary<string, Type> _pages = new();

    public PageService()
            {
                Configure<启动ViewModel, 启动Page>();
                Configure<下载ViewModel, 下载Page>();
                Configure<ModViewModel, ModPage>();
                Configure<ModDownloadDetailViewModel, ModDownloadDetailPage>();
                Configure<SettingsViewModel, SettingsPage>();
                Configure<ModLoader选择ViewModel, ModLoader选择Page>();
                Configure<版本列表ViewModel, 版本列表Page>();
                Configure<版本管理ViewModel, 版本管理Page>();
                Configure<ResourceDownloadViewModel, ResourceDownloadPage>();
                Configure<角色ViewModel, 角色Page>();
                Configure<角色管理ViewModel, 角色管理Page>();
                Configure<错误分析系统ViewModel, 错误分析系统Page>();
            }

    public Type GetPageType(string key)
    {
        Type? pageType;
        lock (_pages)
        {
            if (!_pages.TryGetValue(key, out pageType))
            {
                throw new ArgumentException($"Page not found: {key}. Did you forget to call PageService.Configure?");
            }
        }

        return pageType;
    }

    private void Configure<VM, V>()
        where VM : ObservableObject
        where V : Page
    {
        lock (_pages)
        {
            var key = typeof(VM).FullName!;
            if (_pages.ContainsKey(key))
            {
                throw new ArgumentException($"The key {key} is already configured in PageService");
            }

            var type = typeof(V);
            if (_pages.ContainsValue(type))
            {
                throw new ArgumentException($"This type is already configured with key {_pages.First(p => p.Value == type).Key}");
            }

            _pages.Add(key, type);
        }
    }
}
