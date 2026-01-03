using Microsoft.UI.Xaml.Data;
using System;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 角色类型转换器
    /// </summary>
    public class ProfileTypeConverter : IValueConverter
    {
        /// <summary>
        /// 将角色对象转换为对应的文本
        /// </summary>
        /// <param name="value">角色对象</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="language">语言</param>
        /// <returns>转换后的文本</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MinecraftProfile profile)
            {
                // 根据角色属性判断类型
                if (profile.IsOffline)
                {
                    return "ProfilePage_OfflineProfileText".GetLocalized();
                }
                else if (profile.TokenType == "external")
                {
                    return "ProfilePage_ProfileType_External".GetLocalized();
                }
                else
                {
                    return "ProfilePage_MicrosoftProfileText".GetLocalized();
                }
            }
            return "未知类型";
        }

        /// <summary>
        /// 将文本转换回角色对象（未实现）
        /// </summary>
        /// <param name="value">文本</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="language">语言</param>
        /// <returns>转换后的角色对象</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}