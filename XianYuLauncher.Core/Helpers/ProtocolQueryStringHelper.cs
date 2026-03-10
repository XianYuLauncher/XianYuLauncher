using System.Net;

namespace XianYuLauncher.Core.Helpers;

public static class ProtocolQueryStringHelper
{
    public static Dictionary<string, string> ParseQueryString(string? query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        var trimmed = query.TrimStart('?');

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equalIndex = pair.IndexOf('=');
            if (equalIndex < 0)
            {
                continue;
            }

            var key = WebUtility.UrlDecode(pair[..equalIndex]);
            var value = equalIndex + 1 < pair.Length
                ? WebUtility.UrlDecode(pair[(equalIndex + 1)..])
                : string.Empty;
            result[key] = value;
        }

        return result;
    }
}
