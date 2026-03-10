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

        var queryParams = ProtocolQueryStringHelper.ParseQueryString(uri.Query);

        if (string.Equals(uri.Host, "launch", StringComparison.OrdinalIgnoreCase))
        {
            queryParams.TryGetValue("path", out var targetPath);
            queryParams.TryGetValue("map", out var mapName);
            queryParams.TryGetValue("server", out var serverIp);
            queryParams.TryGetValue("port", out var serverPort);

            command = new LaunchProtocolCommand(uri, targetPath, mapName, serverIp, serverPort);
            return true;
        }

        if (string.Equals(uri.Host, "open", StringComparison.OrdinalIgnoreCase))
        {
            queryParams.TryGetValue("page", out var page);
            command = new NavigateProtocolCommand(uri, page);
            return true;
        }

        if (string.Equals(uri.Host, "navigate", StringComparison.OrdinalIgnoreCase))
        {
            queryParams.TryGetValue("page", out var page);
            command = new NavigateProtocolCommand(uri, page);
            return true;
        }

        return false;
    }
}
