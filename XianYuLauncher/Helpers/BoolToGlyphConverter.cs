using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将布尔值转换为对应的展开/折叠图标
/// </summary>
public class BoolToGlyphConverter : IValueConverter
{
    /// <summary>
    /// 转换方法
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数</param>
    /// <param name="language">语言</param>
    /// <returns>对应的Glyph字符串</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isExpanded)
        {
            // 如果展开，返回下箭头；否则返回右箭头
            return isExpanded ? "▼" : "▶";
        }
        
        // 默认返回右箭头
        return "▶";
    }
    
    /// <summary>
    /// 反向转换方法（未使用）
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}