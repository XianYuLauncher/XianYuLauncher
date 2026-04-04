using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.UI.Xaml.Controls;

namespace XianYuLauncher.Services;

internal interface IPageMapBuilder
{
    void Configure<TViewModel, TPage>()
        where TViewModel : ObservableObject
        where TPage : Page;
}

internal interface IPageMapContributor
{
    void Configure(IPageMapBuilder builder);
}

internal sealed class PageMapBuilder : IPageMapBuilder
{
    private readonly Dictionary<string, Type> _pages;

    public PageMapBuilder(Dictionary<string, Type> pages)
    {
        _pages = pages;
    }

    public void Configure<TViewModel, TPage>()
        where TViewModel : ObservableObject
        where TPage : Page
    {
        var key = typeof(TViewModel).FullName!;
        if (_pages.ContainsKey(key))
        {
            throw new ArgumentException($"The key {key} is already configured in PageService");
        }

        var pageType = typeof(TPage);
        if (_pages.ContainsValue(pageType))
        {
            throw new ArgumentException($"This type is already configured with key {_pages.First(p => p.Value == pageType).Key}");
        }

        _pages.Add(key, pageType);
    }
}

internal sealed class PageMapContributor<TViewModel, TPage> : IPageMapContributor
    where TViewModel : ObservableObject
    where TPage : Page
{
    public void Configure(IPageMapBuilder builder)
    {
        builder.Configure<TViewModel, TPage>();
    }
}