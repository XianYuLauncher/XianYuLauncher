using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Services;

/// <summary>
/// 负责将远程图片 URL 转换为可绑定的 WinUI <see cref="ImageSource"/>。
/// </summary>
public sealed class HttpImageSourceService : IHttpImageSourceService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUiDispatcher _uiDispatcher;

    public HttpImageSourceService(
        IHttpClientFactory httpClientFactory,
        IUiDispatcher uiDispatcher)
    {
        _httpClientFactory = httpClientFactory;
        _uiDispatcher = uiDispatcher;
    }

    public async Task<ImageSource?> LoadFromUrlAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = DefaultTimeout;
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", VersionHelper.GetUserAgent());

        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            return null;
        }

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        SoftwareBitmapSource? imageSource = null;
        await _uiDispatcher.RunOnUiThreadAsync(async () =>
        {
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softwareBitmap);
            imageSource = source;
        });

        return imageSource;
    }
}
