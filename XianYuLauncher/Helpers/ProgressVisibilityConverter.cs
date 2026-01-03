using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers;

public class ProgressVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double progressValue)
        {
            // 当进度值大于0且小于100时显示进度条，否则隐藏
            return progressValue > 0 && progressValue < 100 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}