using Microsoft.Extensions.DependencyInjection;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Extensions;

/// <summary>
/// 认证相关服务（Microsoft、AuthlibInjector、TokenRefresh）的 DI 注册扩展。
/// </summary>
internal static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddHttpClient<MicrosoftAuthService>();

        services.AddSingleton<AuthlibInjectorService>();

        services.AddSingleton<ITokenRefreshService>(sp =>
        {
            var microsoftAuthService = sp.GetRequiredService<MicrosoftAuthService>();
            var authlibInjectorService = sp.GetRequiredService<AuthlibInjectorService>();
            return new TokenRefreshService(microsoftAuthService, authlibInjectorService);
        });

        return services;
    }
}
