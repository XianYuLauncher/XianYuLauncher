using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IProfileDialogService
{
    Task<XianYuLauncher.Core.Services.ExternalProfile?> ShowProfileSelectionDialogAsync(List<XianYuLauncher.Core.Services.ExternalProfile> profiles, string authServer);

    Task<LoginMethodSelectionResult> ShowLoginMethodSelectionDialogAsync(
        string title = "选择登录方式",
        string instruction = "请选择您喜欢的登录方式：",
        string browserDescription = "• 浏览器登录：打开系统默认浏览器进行登录 (推荐)",
        string deviceCodeDescription = "• 设备代码登录：获取代码后手动访问网页输入",
        string browserButtonText = "浏览器登录",
        string deviceCodeButtonText = "设备代码登录",
        string cancelButtonText = "取消");

    Task<SkinModelSelectionResult> ShowSkinModelSelectionDialogAsync(
        string title = "选择皮肤模型",
        string content = "请选择此皮肤适用的人物模型",
        string steveButtonText = "Steve",
        string alexButtonText = "Alex",
        string cancelButtonText = "取消");
}