using Microsoft.Windows.ApplicationModel.Resources;

namespace XianYuLauncher.Helpers;

public static class ResourceExtensions
{
    private static readonly ResourceLoader _resourceLoader = new();

    public static string GetLocalized(this string resourceKey) 
    {
        try
        {
            return _resourceLoader.GetString(resourceKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Resource not found: '{resourceKey}' - {ex.Message}");
            return resourceKey; // 返回键名作为默认值，避免程序崩溃
        }
    }
    
    public static string GetLocalized(this string resourceKey, params object[] args) 
    {
        try
        {
            string resourceValue = _resourceLoader.GetString(resourceKey);
            return string.Format(resourceValue, args);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Resource not found: '{resourceKey}' - {ex.Message}");
            return $"{resourceKey} ({string.Join(", ", args)})"; // 返回键名和参数作为默认值
        }
    }
}
