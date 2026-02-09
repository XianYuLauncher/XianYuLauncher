using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将 MaterialType 枚举转换为 Visibility 的转换器
/// 仅当为 CustomBackground 时返回 Visible
/// </summary>
public class MaterialTypeToCustomBackgroundVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is MaterialType materialType)
        {
            return materialType == MaterialType.CustomBackground ? Visibility.Visible : Visibility.Collapsed;
        }
        
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
