using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.UI.Xaml.Controls;

using XianYuLauncher.Services;

namespace XianYuLauncher.Extensions;

internal static class PageMapServiceCollectionExtensions
{
    public static IServiceCollection AddPageMap<TViewModel, TPage>(this IServiceCollection services)
        where TViewModel : ObservableObject
        where TPage : Page
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPageMapContributor, PageMapContributor<TViewModel, TPage>>());
        return services;
    }
}