using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 从 Minecraft 皮肤纹理中裁剪头像的共享逻辑。
/// 支持 64x64 与 128x128 HD 皮肤，按皮肤尺寸自动计算裁剪区域。
/// </summary>
public static class SkinAvatarHelper
{
    private const int StandardSkinSize = 64;

    /// <summary>
    /// 从已加载的皮肤纹理中裁剪头像区域。
    /// </summary>
    /// <param name="skinBitmap">已加载的皮肤 CanvasBitmap（64x64 或 128x128）</param>
    /// <param name="outputSize">输出头像尺寸，默认 48</param>
    /// <param name="includeOverlay">是否绘制第二层（帽子/头盔）</param>
    /// <param name="uuid">可选，用于保存到缓存</param>
    /// <returns>裁剪后的头像 BitmapImage，失败返回 null</returns>
    public static async Task<BitmapImage?> CropHeadFromSkinAsync(
        CanvasBitmap skinBitmap,
        int outputSize = 48,
        bool includeOverlay = false,
        string? uuid = null)
    {
        try
        {
            var device = skinBitmap.Device;
            var width = (float)skinBitmap.Size.Width;
            var height = (float)skinBitmap.Size.Height;

            // 64x64: head=(8,8,8,8), overlay=(40,8,8,8)
            // 128x128: head=(16,16,16,16), overlay=(80,16,16,16)
            var scale = width / StandardSkinSize;
            var headSize = 8f * scale;
            var headRect = new Rect(8f * scale, 8f * scale, headSize, headSize);
            var overlayRect = new Rect(40f * scale, 8f * scale, headSize, headSize);

            var renderTarget = new CanvasRenderTarget(device, outputSize, outputSize, 96);

            using (var ds = renderTarget.CreateDrawingSession())
            {
                PixelArtRenderHelper.SetAliased(ds);

                PixelArtRenderHelper.DrawNearestNeighbor(
                    ds,
                    skinBitmap,
                    new Rect(0, 0, outputSize, outputSize),
                    headRect);

                if (includeOverlay)
                {
                    PixelArtRenderHelper.DrawNearestNeighbor(
                        ds,
                        skinBitmap,
                        new Rect(0, 0, outputSize, outputSize),
                        overlayRect);
                }
            }

            if (!string.IsNullOrEmpty(uuid))
            {
                try
                {
                    var cacheFolder = await ApplicationData.Current.LocalFolder
                        .CreateFolderAsync(AppDataFileConsts.AvatarCacheFolder, CreationCollisionOption.OpenIfExists);
                    var avatarFile = await cacheFolder.CreateFileAsync($"{uuid}.png", CreationCollisionOption.ReplaceExisting);
                    using (var fileStream = await avatarFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png);
                    }
                }
                catch
                {
                    // 保存缓存失败，不影响主流程
                }
            }

            using (var outputStream = new InMemoryRandomAccessStream())
            {
                await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                outputStream.Seek(0);

                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(outputStream);
                return bitmapImage;
            }
        }
        catch
        {
            return null;
        }
    }
}
