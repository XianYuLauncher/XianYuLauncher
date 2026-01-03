using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 将本地文件路径转换为Uri格式，用于Image控件的Source属性
    /// </summary>
    public class PathToUriConverter : IValueConverter
    {
        /// <summary>
        /// 将本地文件路径转换为Uri
        /// </summary>
        /// <param name="value">输入值（本地文件路径）</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换器参数</param>
        /// <param name="language">语言</param>
        /// <returns>转换后的Uri，如果输入为空则返回null</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    // 检查路径是否已经是Uri格式
                    if (Uri.TryCreate(path, UriKind.Absolute, out Uri uriResult) && 
                        (uriResult.Scheme == Uri.UriSchemeHttp || 
                         uriResult.Scheme == Uri.UriSchemeHttps || 
                         uriResult.Scheme == "ms-appx" ||
                         uriResult.Scheme == "ms-appdata" ||
                         uriResult.Scheme == Uri.UriSchemeFile))
                    {
                        return uriResult;
                    }
                    
                    // 将本地文件路径转换为Uri（添加file:///协议）
                    if (System.IO.Path.IsPathRooted(path))
                    {
                        return new Uri($"file:///{path.Replace('\\', '/')}");
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

        /// <summary>
        /// 将Uri转换回本地文件路径
        /// </summary>
        /// <param name="value">输入值（Uri）</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换器参数</param>
        /// <param name="language">语言</param>
        /// <returns>转换后的本地文件路径，如果输入为空则返回null</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Uri uri)
            {
                try
                {
                    // 如果是本地文件Uri，返回本地路径
                    if (uri.Scheme == Uri.UriSchemeFile)
                    {
                        return uri.LocalPath;
                    }
                    
                    // 其他情况返回Uri字符串
                    return uri.ToString();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Uri转换失败: {ex.Message}");
                    return null;
                }
            }
            return null;
        }
    }
}