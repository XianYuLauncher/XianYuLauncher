using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XMCL2025.Contracts.Services;
using XMCL2025.Contracts.ViewModels;

namespace XMCL2025.ViewModels;

public partial class 联机ViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;

    public 联机ViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void OnNavigatedFrom()
    {
        // TODO: 处理页面离开时的逻辑
    }

    public void OnNavigatedTo(object parameter)
    {
        // TODO: 处理页面加载时的逻辑
    }

    [RelayCommand]
    private void HostGame()
    {
        // TODO: 实现当房主的逻辑
    }

    [RelayCommand]
    private void JoinGame()
    {
        // TODO: 实现当房客的逻辑
    }
}