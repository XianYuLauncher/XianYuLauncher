using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 版本图标像素风渲染的共享处理逻辑，供各页面复用。
/// </summary>
internal static class VersionIconProcessingHelper
{
    /// <summary>
    /// 加载并渲染版本图标（最近邻插值 32×32）。加载失败时回退到默认图标。
    /// </summary>
    public static async Task<BitmapImage?> ProcessAsync(string? iconPath)
    {
        try
        {
            var normalizedPath = VersionIconPathHelper.NormalizeOrDefault(iconPath);
            var device = CanvasDevice.GetSharedDevice();
            using var canvasBitmap = await LoadCanvasBitmapAsync(device, normalizedPath);

            if (canvasBitmap == null)
            {
                if (!string.Equals(normalizedPath, VersionIconPathHelper.DefaultIconPath, StringComparison.OrdinalIgnoreCase))
                {
                    return await ProcessAsync(VersionIconPathHelper.DefaultIconPath);
                }

                return null;
            }

            using var renderTarget = new CanvasRenderTarget(device, 32, 32, 96);
            using (var drawingSession = renderTarget.CreateDrawingSession())
            {
                drawingSession.Clear(Microsoft.UI.Colors.Transparent);
                PixelArtRenderHelper.DrawNearestNeighbor(
                    drawingSession,
                    canvasBitmap,
                    new Windows.Foundation.Rect(0, 0, 32, 32),
                    new Windows.Foundation.Rect(0, 0, canvasBitmap.Size.Width, canvasBitmap.Size.Height));
            }

            using var outputStream = new InMemoryRandomAccessStream();
            await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
            outputStream.Seek(0);

            var bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(outputStream);
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从给定路径（逻辑资源路径、绝对路径或 file:// URI）加载 CanvasBitmap。
    /// </summary>
    public static async Task<CanvasBitmap?> LoadCanvasBitmapAsync(CanvasDevice device, string iconPath)
    {
        try
        {
            var file = await AppAssetResolver.GetStorageFileAsync(iconPath);
            using var stream = await file.OpenReadAsync();
            return await CanvasBitmap.LoadAsync(device, stream);
        }
        catch
        {
            return null;
        }
    }
}
