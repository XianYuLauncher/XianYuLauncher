using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IProfileDialogService
{
    Task<XianYuLauncher.Core.Services.ExternalProfile?> ShowProfileSelectionDialogAsync(List<XianYuLauncher.Core.Services.ExternalProfile> profiles, string authServer);

    Task<LoginMethodSelectionResult> ShowLoginMethodSelectionDialogAsync();

    Task<SkinModelSelectionResult> ShowSkinModelSelectionDialogAsync();
}
