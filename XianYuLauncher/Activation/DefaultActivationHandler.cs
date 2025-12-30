using Microsoft.UI.Xaml;

using XMCL2025.Contracts.Services;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.ViewModels;

namespace XMCL2025.Activation;

public class DefaultActivationHandler : ActivationHandler<LaunchActivatedEventArgs>
{
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;

    public DefaultActivationHandler(INavigationService navigationService, ILocalSettingsService localSettingsService)
    {
        _navigationService = navigationService;
        _localSettingsService = localSettingsService;
    }

    protected override bool CanHandleInternal(LaunchActivatedEventArgs args)
    {
        // None of the ActivationHandlers has handled the activation.
        return _navigationService.Frame?.Content == null;
    }

    protected async override Task HandleInternalAsync(LaunchActivatedEventArgs args)
        {
            // 检查是否已经完成新手教程
            bool isTutorialCompleted = await _localSettingsService.ReadSettingAsync<bool>("TutorialCompleted");
            
            // 添加Debug输出，以便在Visual Studio中查看
            System.Diagnostics.Debug.WriteLine($"[首次启动检查] TutorialCompleted值: {isTutorialCompleted}");
            
            if (!isTutorialCompleted)
            {
                // 首次启动，导航到新手教程页面
                System.Diagnostics.Debug.WriteLine($"[首次启动检查] 首次启动，导航到新手教程页面");
                _navigationService.NavigateTo(typeof(TutorialPageViewModel).FullName!, args.Arguments);
            }
            else
            {
                // 直接导航到正常的启动页面
                System.Diagnostics.Debug.WriteLine($"[首次启动检查] 非首次启动，导航到启动页面");
                _navigationService.NavigateTo(typeof(LaunchViewModel).FullName!, args.Arguments);
            }

            await Task.CompletedTask;
        }
}
