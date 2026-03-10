using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Features.Protocol;

public sealed class ProtocolCommandParser : IProtocolCommandParser
{
    public bool TryParse(Uri uri, out ProtocolCommand? command)
    {
        command = null;

        if (!string.Equals(uri.Scheme, "xianyulauncher", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(uri.Host, "launch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var queryParams = ProtocolQueryStringHelper.ParseQueryString(uri.Query);
        queryParams.TryGetValue("path", out var targetPath);
        queryParams.TryGetValue("map", out var mapName);
        queryParams.TryGetValue("server", out var serverIp);
        queryParams.TryGetValue("port", out var serverPort);

        command = new LaunchProtocolCommand(uri, targetPath, mapName, serverIp, serverPort);
        return true;
    }
}
