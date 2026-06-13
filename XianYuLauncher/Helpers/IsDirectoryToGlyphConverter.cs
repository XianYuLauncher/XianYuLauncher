using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将 IsDirectory 属性转换为对应的 FontIcon Glyph
/// </summary>
public class IsDirectoryToGlyphConverter : IValueConverter
{
    /// <summary>
    /// 转换方法
    /// </summary>
    /// <param name="value">IsDirectory 属性值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数</param>
    /// <param name="language">语言</param>
    /// <returns>对应的 Glyph 字符串</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isDirectory)
        {
            // 如果是目录，返回文件夹图标；否则返回文件图标
            // 使用 Segoe MDL2 Assets 字体中的图标代码
            return isDirectory ? "\uE8B7" : "\uE8A5";
        }
        
        // 默认返回文件图标
        return "\uE8A5";
    }
    
    /// <summary>
    /// 反向转换方法（未使用）
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}