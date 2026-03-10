namespace XianYuLauncher.Features.Protocol;

public sealed class ProtocolCommandDispatcher : IProtocolCommandDispatcher
{
    private readonly IEnumerable<IProtocolCommandHandler> _handlers;

    public ProtocolCommandDispatcher(IEnumerable<IProtocolCommandHandler> handlers)
    {
        _handlers = handlers;
    }

    public async Task<bool> DispatchAsync(ProtocolCommand command)
    {
        var handler = _handlers.FirstOrDefault(h => h.CanHandle(command));
        if (handler == null)
        {
            return false;
        }

        await handler.HandleAsync(command);
        return true;
    }
}
