using System.Threading.Tasks;
using XianYuLauncher.Features.ResourceDownload.ViewModels;

namespace XianYuLauncher.Features.ResourceDownload.Services;

public interface IResourceDownloadTabCoordinator
{
    Task EnsureSelectedTabLoadedAsync(ResourceDownloadHostViewModel viewModel, int selectedIndex);

    bool IsTabLoaded(int tabIndex);
}
