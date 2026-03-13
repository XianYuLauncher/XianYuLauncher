namespace XianYuLauncher.Core.Helpers;

public static class ProtocolNavigationParameterHelper
{
    public static bool TryGetStringParameter(object? parameter, string key, out string value)
    {
        value = string.Empty;

        if (parameter is IReadOnlyDictionary<string, string> readOnlyMap
            && readOnlyMap.TryGetValue(key, out var readOnlyValue)
            && !string.IsNullOrWhiteSpace(readOnlyValue))
        {
            value = readOnlyValue;
            return true;
        }

        if (parameter is IDictionary<string, string> map
            && map.TryGetValue(key, out var mapValue)
            && !string.IsNullOrWhiteSpace(mapValue))
        {
            value = mapValue;
            return true;
        }

        return false;
    }
}