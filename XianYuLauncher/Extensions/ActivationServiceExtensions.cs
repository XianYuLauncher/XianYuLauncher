using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using XianYuLauncher.Activation;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Protocol;
using XianYuLauncher.Services;

namespace XianYuLauncher.Extensions;

/// <summary>
/// 激活与协议相关服务的 DI 注册扩展。
/// </summary>
internal static class ActivationServiceExtensions
{
    public static IServiceCollection AddActivationServices(this IServiceCollection services)
    {
        services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();
        services.AddSingleton<IActivationService, ActivationService>();

        services.AddSingleton<IProtocolCommandParser, ProtocolCommandParser>();
        services.AddSingleton<IProtocolCommandDispatcher, ProtocolCommandDispatcher>();
        services.AddSingleton<IProtocolActivationService, ProtocolActivationService>();
        services.AddSingleton<IProtocolCommandHandler, LaunchProtocolCommandHandler>();
        services.AddSingleton<IProtocolCommandHandler, NavigateProtocolCommandHandler>();

        return services;
    }
}
