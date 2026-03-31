using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IProfileDialogService
{
    Task<XianYuLauncher.Core.Services.ExternalProfile?> ShowProfileSelectionDialogAsync(List<XianYuLauncher.Core.Services.ExternalProfile> profiles, string authServer);

    Task<MinecraftProfile?> ShowLauncherProfileSelectionDialogAsync(
        List<MinecraftProfile> profiles,
        string title,
        string primaryButtonText,
        string closeButtonText);

    Task<LoginMethodSelectionResult> ShowLoginMethodSelectionDialogAsync(
        string? title = null,
        string? instruction = null,
        string? browserDescription = null,
        string? deviceCodeDescription = null,
        string? browserButtonText = null,
        string? deviceCodeButtonText = null,
        string? cancelButtonText = null);

    Task<SkinModelSelectionResult> ShowSkinModelSelectionDialogAsync();
}
