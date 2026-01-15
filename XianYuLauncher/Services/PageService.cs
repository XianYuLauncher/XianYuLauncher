using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.UI.Xaml.Controls;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher.Services;

public class PageService : IPageService
{
    private readonly Dictionary<string, Type> _pages = new();

    public PageService()
                {
                    Configure<LaunchViewModel, LaunchPage>();
                    // Configure<下载ViewModel, 下载Page>();
                    // Configure<ModViewModel, ModPage>();
                    Configure<ModDownloadDetailViewModel, ModDownloadDetailPage>();
                    Configure<SettingsViewModel, SettingsPage>();
                    Configure<ModLoaderSelectorViewModel, ModLoaderSelectorPage>();
                    Configure<VersionListViewModel, VersionListPage>();
                    Configure<VersionManagementViewModel, VersionManagementPage>();
                    Configure<WorldManagementViewModel, WorldManagementPage>();
                    Configure<ResourceDownloadViewModel, ResourceDownloadPage>();
                    Configure<CharacterViewModel, CharacterPage>();
                    Configure<CharacterManagementViewModel, CharacterManagementPage>();
                    Configure<ErrorAnalysisViewModel, ErrorAnalysisPage>();
                    Configure<TutorialPageViewModel, TutorialPage>();
                    Configure<MultiplayerViewModel, MultiplayerPage>();
                    Configure<MultiplayerLobbyViewModel, MultiplayerLobbyPage>();
                    Configure<NewsListViewModel, NewsListPage>();
                    Configure<NewsDetailViewModel, NewsDetailPage>();
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
