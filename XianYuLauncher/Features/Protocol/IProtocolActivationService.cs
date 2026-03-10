using Microsoft.Windows.AppLifecycle;

namespace XianYuLauncher.Features.Protocol;

public interface IProtocolActivationService
{
    Task<bool> TryHandleAsync(AppActivationArguments appActivationArguments);
}
