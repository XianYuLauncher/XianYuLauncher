using System;
using Microsoft.UI.Xaml.Data;

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
            switch (modLoaderName)
            {
                case "Fabric":
                    return "ms-appx:///Assets/Icons/Download_Options/Fabric/fabric_Icon.png";
                case "Forge":
                    return "ms-appx:///Assets/Icons/Download_Options/Forge/MinecraftForge_Icon.jpg";
                case "NeoForge":
                    return "ms-appx:///Assets/Icons/Download_Options/NeoForge/NeoForge_Icon.png";
                default:
                    return "ms-appx:///Assets/Icons/Download_Options/Fabric/fabric_Icon.png"; // 默认返回Fabric图标
            }
        }
        return "ms-appx:///Assets/Icons/Download_Options/Fabric/fabric_Icon.png"; // 默认返回Fabric图标
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