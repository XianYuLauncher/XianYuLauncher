using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Services;

internal sealed class PageService : IPageService
{
    private readonly Dictionary<string, Type> _pages = new();

    public PageService(IEnumerable<IPageMapContributor> contributors)
    {
        var builder = new PageMapBuilder(_pages);
        foreach (var contributor in contributors)
        {
            contributor.Configure(builder);
        }
    }

    public Type GetPageType(string key)
    {
        Type? pageType;
        lock (_pages)
        {
            if (!_pages.TryGetValue(key, out pageType))
            {
                throw new ArgumentException($"Page not found: {key}. Did you forget to register a page map contributor?");
            }
        }

        return pageType;
    }
}
