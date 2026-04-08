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
    private static ReadOnlySpan<byte> Gif87aHeader => "GIF87a"u8;
    private static ReadOnlySpan<byte> Gif89aHeader => "GIF89a"u8;

    /// <summary>单张图标允许的最大字节数（5 MB），防止超大文件导致 OOM。</summary>
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    /// <summary>图标解码的目标边长（px），高 DPI 下实际显示 80×80，此处留 2× 余量。</summary>
    private const uint DecodeTargetSize = 160;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUiDispatcher _uiDispatcher;

    public HttpImageSourceService(
        IHttpClientFactory httpClientFactory,
        IUiDispatcher uiDispatcher)
    {
        _httpClientFactory = httpClientFactory;
        _uiDispatcher = uiDispatcher;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 加载失败（HTTP 非 2xx、超时、解码错误、超出大小限制等）时返回 <c>null</c>，
    /// 不会向调用方抛出异常。
    /// </remarks>
    public async Task<ImageSource?> LoadFromUrlAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = DefaultTimeout;
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", VersionHelper.GetUserAgent());

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // 根据 Content-Length 做前置大小检查，避免读入超大文件
            if (response.Content.Headers.ContentLength is { } contentLength && contentLength > MaxImageSizeBytes)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0 || bytes.Length > MaxImageSizeBytes)
            {
                return null;
            }

            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            if (IsGifImage(uri, response.Content.Headers.ContentType?.MediaType, bytes))
            {
                BitmapImage? animatedImageSource = null;
                await _uiDispatcher.RunOnUiThreadAsync(async () =>
                {
                    stream.Seek(0);

                    var source = new BitmapImage();
                    await source.SetSourceAsync(stream);
                    animatedImageSource = source;
                });

                return animatedImageSource;
            }

            var decoder = await BitmapDecoder.CreateAsync(stream);

            // 等比缩放到目标尺寸，避免按原始分辨率解码浪费 CPU/内存
            var scaleRatio = Math.Min((double)DecodeTargetSize / decoder.PixelWidth, (double)DecodeTargetSize / decoder.PixelHeight);
            if (scaleRatio > 1.0) scaleRatio = 1.0; // 小图不放大
            var transform = new BitmapTransform
            {
                ScaledWidth = (uint)Math.Round(decoder.PixelWidth * scaleRatio),
                ScaledHeight = (uint)Math.Round(decoder.PixelHeight * scaleRatio),
                InterpolationMode = BitmapInterpolationMode.Fant
            };

            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);

            SoftwareBitmapSource? imageSource = null;
            await _uiDispatcher.RunOnUiThreadAsync(async () =>
            {
                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(softwareBitmap);
                imageSource = source;
            });

            return imageSource;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsGifImage(Uri uri, string? mediaType, ReadOnlySpan<byte> bytes)
    {
        if (string.Equals(mediaType, "image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (uri.AbsolutePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return bytes.Length >= Gif87aHeader.Length
            && (bytes[..Gif87aHeader.Length].SequenceEqual(Gif87aHeader)
                || bytes[..Gif89aHeader.Length].SequenceEqual(Gif89aHeader));
    }
}
