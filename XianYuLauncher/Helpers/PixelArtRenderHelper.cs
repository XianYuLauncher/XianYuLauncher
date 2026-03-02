using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Windows.Foundation;

namespace XianYuLauncher.Helpers;

public static class PixelArtRenderHelper
{
    public static void SetAliased(CanvasDrawingSession drawingSession)
    {
        drawingSession.Antialiasing = CanvasAntialiasing.Aliased;
    }

    public static void DrawNearestNeighbor(
        CanvasDrawingSession drawingSession,
        ICanvasImage image,
        Rect destinationRect,
        Rect sourceRect,
        float opacity = 1.0f)
    {
        drawingSession.DrawImage(
            image,
            destinationRect,
            sourceRect,
            opacity,
            CanvasImageInterpolation.NearestNeighbor);
    }
}