using Microsoft.UI.Xaml.Media;
using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services;

/// <summary>
/// 将远程 HTTP 图片加载为 WinUI 可直接绑定的 <see cref="ImageSource"/>。
/// </summary>
public interface IHttpImageSourceService
{
    Task<ImageSource?> LoadFromUrlAsync(string? url, CancellationToken cancellationToken = default);
}
