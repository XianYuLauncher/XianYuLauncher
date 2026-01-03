using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Windows.UI.Xaml;

namespace XianYuLauncher.Helpers;

public class StringNullOrEmptyToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool result;
        if (value is string strValue)
        {
            // 如果字符串不为空或null，返回true（启用按钮）
            result = !string.IsNullOrEmpty(strValue);
        }
        else
        {
            result = false;
        }

        // 检查目标类型以确定返回值类型
        if (targetType == typeof(Visibility))
        {
            // 如果是inverse模式，反转可见性逻辑
            bool isInverse = parameter is string param && param.Equals("inverse", StringComparison.OrdinalIgnoreCase);
            bool shouldBeVisible = isInverse ? !result : result;
            return shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        // 对于布尔类型，根据参数决定是否反转
        if (parameter is string inverseParam && inverseParam.Equals("inverse", StringComparison.OrdinalIgnoreCase))
        {
            return !result;
        }

        return result;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}