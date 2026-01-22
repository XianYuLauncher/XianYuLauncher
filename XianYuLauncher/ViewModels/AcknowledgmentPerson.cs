using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.ViewModels;

/// <summary>
/// 鸣谢人员数据模型
/// </summary>
public partial class AcknowledgmentPerson : ObservableObject
{
    /// <summary>
    /// 姓名
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// 支持信息
    /// </summary>
    [ObservableProperty]
    private string _supportInfo;

    /// <summary>
    /// 头像路径
    /// </summary>
    [ObservableProperty]
    private string _avatar;

    public AcknowledgmentPerson(string name, string supportInfo, string avatar = "ms-appx:///Assets/Icons/Avatars/Steve.png")
    {
        _name = name;
        _supportInfo = supportInfo;
        _avatar = avatar;
    }
}