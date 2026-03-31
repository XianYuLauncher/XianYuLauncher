using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface ICommunityResourceUpdateService
{
    Task<string> StartUpdateAsync(
        CommunityResourceUpdateRequest request,
        CancellationToken cancellationToken = default);
}