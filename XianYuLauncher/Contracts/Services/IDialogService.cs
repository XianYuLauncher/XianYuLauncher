using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Contracts.Services;

public interface IDialogService :
    ICommonDialogService,
    IProgressDialogService,
    IApplicationDialogService,
    IProfileDialogService,
    IResourceDialogService,
    ISelectionDialogService,
    ICrashReportDialogService
{
}