namespace XianYuLauncher.Core.Helpers;

public static class ProtocolPathSecurityHelper
{
    public static bool IsUncPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.StartsWith("\\\\", StringComparison.Ordinal) ||
               path.StartsWith("//", StringComparison.Ordinal);
    }
}
