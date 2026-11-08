using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LiteSS;

internal static class ScreenCapture
{
    /// <summary>Captures every monitor into one image using virtual-screen coordinates.</summary>
    public static Bitmap CaptureVirtualScreen() => CaptureBounds(SystemInformation.VirtualScreen);

    /// <summary>Captures the primary monitor.</summary>
    public static Bitmap CapturePrimaryScreen() =>
        CaptureBounds(Screen.PrimaryScreen?.Bounds ?? SystemInformation.VirtualScreen);

    private static Bitmap CaptureBounds(Rectangle bounds)
    {
        // 32bppRgb avoids the transparent-PNG problem CopyFromScreen has with an alpha channel.
        var bitmap = new Bitmap(
            Math.Max(bounds.Width, 1),
            Math.Max(bounds.Height, 1),
            PixelFormat.Format32bppRgb);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }
}
