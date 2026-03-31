using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface ICommunityResourceUpdateCheckService
{
    Task<CommunityResourceUpdateCheckResult> CheckAsync(
        CommunityResourceUpdateCheckRequest request,
        CancellationToken cancellationToken = default);
}