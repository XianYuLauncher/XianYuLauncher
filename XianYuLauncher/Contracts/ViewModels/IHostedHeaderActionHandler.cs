using System.Threading.Tasks;

using XianYuLauncher.Models;

namespace XianYuLauncher.Contracts.ViewModels;

public interface IHostedHeaderActionHandler
{
    void ApplyBuiltInIcon(VersionIconOption iconOption);

    Task RequestCustomIconAsync();
}