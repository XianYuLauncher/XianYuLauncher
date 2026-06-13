using Microsoft.UI.Xaml.Data;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将本地文件路径转换为 Uri 格式，用于 Image 控件的 Source 属性
/// </summary>
public class PathToUriConverter : IValueConverter
{
    /// <summary>
    /// 将本地文件路径转换为 Uri
    /// </summary>
    /// <param name="value">输入值（本地文件路径）</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="language">语言</param>
    /// <returns>转换后的 Uri，如果输入为空则返回 null</returns>
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                if (AppAssetResolver.IsAppAssetPath(path))
                {
                    return AppAssetResolver.ToUri(path);
                }

                // 检查路径是否已经是 Uri 格式
                if (Uri.TryCreate(path, UriKind.Absolute, out var uriResult) &&
                    uriResult is not null &&
                    (uriResult.Scheme == Uri.UriSchemeHttp || 
                     uriResult.Scheme == Uri.UriSchemeHttps || 
                     uriResult.Scheme == "ms-appdata" ||
                     uriResult.Scheme == Uri.UriSchemeFile))
                {
                    return uriResult;
                }
                    
                // 将本地文件路径转换为 Uri（添加 file:///协议）
                if (System.IO.Path.IsPathRooted(path))
                {
                    // 添加时间戳参数以防止缓存
                    return AppendCacheBust(new Uri(System.IO.Path.GetFullPath(path)));
                }
                    
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"路径转换失败: {ex.Message}");
                return null;
            }
        }
        return null;
    }

    private static Uri AppendCacheBust(Uri uri)
    {
        var builder = new UriBuilder(uri) { Query = $"t={DateTime.Now.Ticks}" };
        return builder.Uri;
    }

    /// <summary>
    /// 将 Uri 转换回本地文件路径
    /// </summary>
    /// <param name="value">输入值（Uri）</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="language">语言</param>
    /// <returns>转换后的本地文件路径，如果输入为空则返回 null</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        if (value is Uri uri)
        {
            try
            {
                // 如果是本地文件 Uri，返回本地路径
                if (uri.Scheme == Uri.UriSchemeFile)
                {
                    return uri.LocalPath;
                }
                    
                // 其他情况返回 Uri 字符串
                return uri.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Uri 转换失败: {ex.Message}");
                return null;
            }
        }
        return null;
    }
}