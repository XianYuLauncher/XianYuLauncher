namespace XianYuLauncher.Features.Protocol;

public interface IProtocolCommandHandler
{
    bool CanHandle(ProtocolCommand command);

    Task HandleAsync(ProtocolCommand command);
}
