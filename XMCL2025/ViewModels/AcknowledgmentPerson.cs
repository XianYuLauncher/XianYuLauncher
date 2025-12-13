using CommunityToolkit.Mvvm.ComponentModel;

namespace XMCL2025.ViewModels;

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

    public AcknowledgmentPerson(string name, string supportInfo)
    {
        _name = name;
        _supportInfo = supportInfo;
    }
}