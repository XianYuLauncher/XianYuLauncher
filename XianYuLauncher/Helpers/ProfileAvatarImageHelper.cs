using System.IO;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Helpers;

public static class ProfileAvatarImageHelper
{
    public static async Task<BitmapImage> CreateDefaultProfileAvatarAsync(int outputSize = 32)
    {
        var device = CanvasDevice.GetSharedDevice();
        var file = await AppAssetResolver.GetStorageFileAsync(AppAssetResolver.DefaultAvatarAssetPath);

        using var stream = await file.OpenReadAsync();
        var originalBitmap = await CanvasBitmap.LoadAsync(device, stream);

        var renderTarget = new CanvasRenderTarget(device, outputSize, outputSize, 96);
        using (var drawingSession = renderTarget.CreateDrawingSession())
        {
            PixelArtRenderHelper.SetAliased(drawingSession);
            PixelArtRenderHelper.DrawNearestNeighbor(
                drawingSession,
                originalBitmap,
                new Windows.Foundation.Rect(0, 0, outputSize, outputSize),
                originalBitmap.Bounds);
        }

        using var outputStream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
        outputStream.Seek(0);

        var bitmapImage = new BitmapImage();
        await bitmapImage.SetSourceAsync(outputStream);
        return bitmapImage;
    }

    public static async Task<BitmapImage?> CreateProfileAvatarFromSkinAsync(byte[] skinBytes, int outputSize = 32, bool includeOverlay = true)
    {
        try
        {
            var device = CanvasDevice.GetSharedDevice();
            using var stream = new MemoryStream(skinBytes);
            var originalBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
            return await SkinAvatarHelper.CropHeadFromSkinAsync(originalBitmap, outputSize, includeOverlay);
        }
        catch
        {
            return null;
        }
    }
}