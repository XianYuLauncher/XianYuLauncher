namespace XianYuLauncher.Features.Protocol;

public interface IProtocolCommandDispatcher
{
    Task<bool> DispatchAsync(ProtocolCommand command);
}
