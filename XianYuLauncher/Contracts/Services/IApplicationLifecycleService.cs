using System;
using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services;

/// <summary>
/// 应用生命周期与外部启动相关能力抽象。
/// </summary>
public interface IApplicationLifecycleService
{
    Task RestartApplicationAsync();

    Task ShutdownApplicationAsync();

    Task<bool> OpenFolderAsync(string folderPath);

    bool OpenFolderInExplorer(string folderPath);

    Task<bool> OpenUriAsync(Uri uri);
}
