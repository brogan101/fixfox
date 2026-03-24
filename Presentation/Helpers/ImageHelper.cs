using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HelpDesk.Presentation.Helpers;

/// <summary>
/// Loads the FixFox logo and removes the black background by replacing
/// near-black pixels with transparent using a WriteableBitmap pass.
/// </summary>
public static class ImageHelper
{
    private static BitmapSource? _logoTransparent;

    /// <summary>Returns the FixFox logo with black background removed, cached after first call.</summary>
    public static BitmapSource GetLogoTransparent()
    {
        if (_logoTransparent is not null) return _logoTransparent;

        try
        {
            var uri = new Uri("pack://application:,,,/FixFoxLogo.png", UriKind.Absolute);
            var original = new BitmapImage(uri);
            _logoTransparent = RemoveNearBlack(original, threshold: 40);
        }
        catch
        {
            // Fallback: return a 1x1 transparent bitmap so the app doesn't crash
            var fallback = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
            _logoTransparent = fallback;
        }
        return _logoTransparent;
    }

    /// <summary>
    /// Replaces pixels whose R, G, and B are all below <paramref name="threshold"/>
    /// with fully transparent pixels. Preserves all other pixels.
    /// </summary>
    private static BitmapSource RemoveNearBlack(BitmapSource source, byte threshold)
    {
        // Convert to Bgra32 so we can manipulate each pixel
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int w = converted.PixelWidth;
        int h = converted.PixelHeight;
        int stride = w * 4; // 4 bytes per pixel (BGRA)
        var pixels = new byte[h * stride];
        converted.CopyPixels(pixels, stride, 0);

        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte b = pixels[i];
            byte g = pixels[i + 1];
            byte r = pixels[i + 2];

            if (r < threshold && g < threshold && b < threshold)
            {
                // Make transparent
                pixels[i]     = 0; // B
                pixels[i + 1] = 0; // G
                pixels[i + 2] = 0; // R
                pixels[i + 3] = 0; // A
            }
        }

        var result = BitmapSource.Create(w, h,
            source.DpiX, source.DpiY,
            PixelFormats.Bgra32, null,
            pixels, stride);
        result.Freeze();
        return result;
    }
}
