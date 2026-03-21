using XianYuLauncher.Core.Models;
using XianYuLauncher.Models;

namespace XianYuLauncher.Contracts.Services;

public interface IDownloadTaskPresentationService
{
    DownloadTaskPresentation Resolve(DownloadTaskInfo taskInfo);
}
