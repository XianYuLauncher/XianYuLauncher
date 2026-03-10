using Microsoft.Windows.AppLifecycle;

namespace XianYuLauncher.Features.Protocol;

public sealed class ProtocolActivationService : IProtocolActivationService
{
    private readonly IProtocolCommandParser _parser;
    private readonly IProtocolCommandDispatcher _dispatcher;

    public ProtocolActivationService(
        IProtocolCommandParser parser,
        IProtocolCommandDispatcher dispatcher)
    {
        _parser = parser;
        _dispatcher = dispatcher;
    }

    public async Task<bool> TryHandleAsync(AppActivationArguments appActivationArguments)
    {
        if (appActivationArguments.Kind != ExtendedActivationKind.Protocol)
        {
            return false;
        }

        if (appActivationArguments.Data is not Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs protocolArgs)
        {
            return false;
        }

        if (!_parser.TryParse(protocolArgs.Uri, out var command) || command == null)
        {
            return false;
        }

        return await _dispatcher.DispatchAsync(command);
    }
}
