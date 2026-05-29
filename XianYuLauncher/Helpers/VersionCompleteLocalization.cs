using XianYuLauncher.Core.Constants;

namespace XianYuLauncher.Helpers;

public static class VersionCompleteLocalization
{
    public static string Localize(string? keyOrText)
    {
        if (string.IsNullOrEmpty(keyOrText))
        {
            return string.Empty;
        }

        if (!VersionCompleteStageKeys.IsResourceKey(keyOrText))
        {
            return keyOrText;
        }

        return keyOrText.GetLocalized();
    }

    public static string LocalizeCurrentFile(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var separatorIndex = value.IndexOf(':');
        if (separatorIndex > 0 &&
            VersionCompleteStageKeys.IsResourceKey(value[..separatorIndex]))
        {
            var key = value[..separatorIndex];
            var arg = value[(separatorIndex + 1)..];
            return string.Format(key.GetLocalized(), arg);
        }

        if (VersionCompleteStageKeys.IsResourceKey(value))
        {
            return value.GetLocalized();
        }

        var display = value.Length > 8 ? value[..8] + "..." : value;
        return string.Format("Dialog_VersionComplete_AssetHash_Format".GetLocalized(), display);
    }
}