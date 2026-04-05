using Newtonsoft.Json;

namespace XianYuLauncher.Core.Helpers;

public static class LocalSettingsStoredStringCompatibilityHelper
{
    public static string UnwrapStoredString(string rawValue)
    {
        if (rawValue.Length < 2 || rawValue[0] != '"' || rawValue[^1] != '"')
        {
            return rawValue;
        }

        try
        {
            return JsonConvert.DeserializeObject<string>(rawValue) ?? string.Empty;
        }
        catch (JsonException)
        {
            return rawValue;
        }
    }
}