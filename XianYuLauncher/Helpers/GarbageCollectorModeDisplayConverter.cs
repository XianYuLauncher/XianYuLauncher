using Microsoft.UI.Xaml.Data;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 垃圾回收器模式显示转换器。
/// 仅对 Auto 做本地化，其他术语保持原文。
/// </summary>
public class GarbageCollectorModeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string mode && mode.Equals(GarbageCollectorModeHelper.Auto, StringComparison.OrdinalIgnoreCase))
        {
            return "GarbageCollectorMode_Auto".GetLocalized();
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string text && text.Equals("GarbageCollectorMode_Auto".GetLocalized(), StringComparison.Ordinal))
        {
            return GarbageCollectorModeHelper.Auto;
        }

        return value?.ToString() ?? string.Empty;
    }
}
