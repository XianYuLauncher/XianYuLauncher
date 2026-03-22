using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface ISelectionDialogService
{
    Task<SettingsCustomSourceDialogResult?> ShowSettingsCustomSourceDialogAsync(SettingsCustomSourceDialogRequest request);

    Task<AddServerDialogResult?> ShowAddServerDialogAsync(string defaultName = "Minecraft Server");
}