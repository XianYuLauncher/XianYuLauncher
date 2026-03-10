namespace XianYuLauncher.Features.Protocol;

public interface IProtocolCommandParser
{
    bool TryParse(Uri uri, out ProtocolCommand? command);
}
