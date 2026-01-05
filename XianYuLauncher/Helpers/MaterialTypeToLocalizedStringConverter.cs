using Microsoft.UI.Xaml.Data;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将 MaterialType 枚举转换为本地化字符串的转换器
/// </summary>
public class MaterialTypeToLocalizedStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is MaterialType materialType)
        {
            // 根据枚举值返回对应的本地化资源键
            string resourceKey = $"MaterialType_{materialType}";
            return resourceKey.GetLocalized();
        }
        
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
