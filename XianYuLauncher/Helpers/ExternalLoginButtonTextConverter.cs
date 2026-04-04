using System;
using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 根据外置登录状态返回教程页按钮文本。
/// </summary>
public sealed class ExternalLoginButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isLoggedIn && isLoggedIn)
        {
            return "TutorialPage_ExternalLoginButton_LoggedIn".GetLocalized();
        }

        return "TutorialPage_ExternalLoginButton_Login".GetLocalized();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}