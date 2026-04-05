using System;
using Microsoft.UI.Xaml.Data;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 转换器：根据ModLoader名称返回对应的图标URL
/// </summary>
public class ModLoaderToIconConverter : IValueConverter
{
    /// <summary>
    /// 将ModLoader名称转换为图标URL
    /// </summary>
    /// <param name="value">ModLoader名称</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数</param>
    /// <param name="language">语言</param>
    /// <returns>图标URL</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string modLoaderName)
        {
            if (AppAssetResolver.TryGetLoaderIconAssetPath(modLoaderName, out var iconAssetPath))
            {
                return AppAssetResolver.ToUriString(iconAssetPath);
            }

            return AppAssetResolver.ToUriString(AppAssetResolver.FabricIconAssetPath);
        }

        return AppAssetResolver.ToUriString(AppAssetResolver.FabricIconAssetPath);
    }

    /// <summary>
    /// 反向转换，不支持
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数</param>
    /// <param name="language">语言</param>
    /// <returns>不支持</returns>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}