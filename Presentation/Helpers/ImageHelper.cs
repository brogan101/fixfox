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
    private static string? _cachedLogoPath;

    /// <summary>Returns the FixFox logo with black background removed, cached after first call.</summary>
    public static BitmapSource GetLogoTransparent(string? customPath = null)
    {
        if (_logoTransparent is not null
            && string.Equals(_cachedLogoPath, customPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            return _logoTransparent;

        try
        {
            BitmapSource original;
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            {
                var custom = new BitmapImage();
                custom.BeginInit();
                custom.CacheOption = BitmapCacheOption.OnLoad;
                custom.UriSource = new Uri(customPath, UriKind.Absolute);
                custom.EndInit();
                custom.Freeze();
                original = custom;
            }
            else
            {
                var uri = new Uri("pack://application:,,,/FixFoxLogo.png", UriKind.Absolute);
                original = new BitmapImage(uri);
            }
            _logoTransparent = RemoveNearBlack(original, threshold: 40);
            _cachedLogoPath = customPath ?? string.Empty;
        }
        catch
        {
            // Fallback: return a 1x1 transparent bitmap so the app doesn't crash
            var fallback = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
            _logoTransparent = fallback;
            _cachedLogoPath = customPath ?? string.Empty;
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
