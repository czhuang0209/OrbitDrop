using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbitDrop.Core;

namespace OrbitDrop.Conversion.Imaging;

public sealed record ImageFormat(string Name, string Extension, bool SupportsAlpha);

/// <summary>
/// Image engine on the Windows Imaging Component (via WPF imaging). Built into Windows: no dependency to ship.
/// Must be used from an STA thread (see <see cref="StaWorker"/>).
/// Limitations (documented in the capability matrix): EXIF orientation is not applied, animated GIF/multi-page TIFF
/// use the first frame, colour profiles are not carried over.
/// </summary>
public static class WicImageCodec
{
    public static readonly ImageFormat Png = new("PNG", ".png", true);
    public static readonly ImageFormat Jpg = new("JPG", ".jpg", false);
    public static readonly ImageFormat Bmp = new("BMP", ".bmp", false);
    public static readonly ImageFormat Tiff = new("TIFF", ".tiff", true);
    public static readonly ImageFormat Gif = new("GIF", ".gif", true);

    /// <summary>Output formats, in wheel order.</summary>
    public static IReadOnlyList<ImageFormat> OutputFormats { get; } = [Png, Jpg, Bmp, Tiff, Gif];

    private static readonly Dictionary<string, ImageFormat> InputByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = Png, [".jpg"] = Jpg, [".jpeg"] = Jpg, [".jpe"] = Jpg, [".jfif"] = Jpg,
        [".bmp"] = Bmp, [".dib"] = Bmp, [".tif"] = Tiff, [".tiff"] = Tiff, [".gif"] = Gif,
    };

    public static ImageFormat? FormatOf(string path) => InputByExtension.GetValueOrDefault(Path.GetExtension(path));

    public static bool IsSupportedInput(string path) => InputByExtension.ContainsKey(Path.GetExtension(path));

    public static IEnumerable<string> InputExtensions => InputByExtension.Keys;

    /// <summary>Decodes the first frame and normalises to BGRA32 (drops metadata/colour contexts that not every encoder accepts).</summary>
    public static BitmapSource Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0) throw new InvalidDataException("The image contains no frames.");
        var bgra = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        bgra.Freeze();
        return bgra;
    }

    public static void Encode(BitmapSource image, ImageFormat format, Stream stream, int jpegQuality)
    {
        BitmapEncoder encoder = format.Extension switch
        {
            ".png" => new PngBitmapEncoder(),
            ".jpg" => new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 1, 100) },
            ".bmp" => new BmpBitmapEncoder(),
            ".tiff" => new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw },
            ".gif" => new GifBitmapEncoder(),
            _ => throw new NotSupportedException($"No encoder for {format.Name}"),
        };
        // Formats without alpha: composite onto white instead of letting transparent pixels turn black.
        encoder.Frames.Add(BitmapFrame.Create(format.SupportsAlpha ? image : FlattenOnWhite(image)));
        encoder.Save(stream);
    }

    /// <summary>Writes <paramref name="image"/> next to <paramref name="source"/> with full SafeOutput guarantees.</summary>
    public static string WriteOutput(BitmapSource image, string source, ImageFormat format, string? suffix,
        OperationContext context, CancellationToken ct)
    {
        int w = image.PixelWidth, h = image.PixelHeight;
        return SafeOutput.Write(source, context.Output, format.Extension, suffix,
            write: s => Encode(image, format, s, context.JpegQuality),
            validate: temp =>
            {
                var check = Load(temp);
                return check.PixelWidth == w && check.PixelHeight == h
                    ? null
                    : $"decoded as {check.PixelWidth}×{check.PixelHeight}, expected {w}×{h}";
            },
            ct);
    }

    public static BitmapSource FlattenOnWhite(BitmapSource source)
    {
        if (source.Format == PixelFormats.Gray8) return source;
        BitmapSource bgra = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        bgra.CopyPixels(px, stride, 0);
        var rgb = new byte[w * 3 * h];
        for (int i = 0, j = 0; i < px.Length; i += 4, j += 3)
        {
            int a = px[i + 3], inv = 255 - a;
            rgb[j] = (byte)((px[i] * a + 255 * inv) / 255);
            rgb[j + 1] = (byte)((px[i + 1] * a + 255 * inv) / 255);
            rgb[j + 2] = (byte)((px[i + 2] * a + 255 * inv) / 255);
        }
        var result = BitmapSource.Create(w, h, bgra.DpiX, bgra.DpiY, PixelFormats.Bgr24, null, rgb, w * 3);
        result.Freeze();
        return result;
    }

    public static BitmapSource Transform(BitmapSource source, Transform transform)
    {
        var t = new TransformedBitmap(source, transform);
        t.Freeze();
        return t;
    }

    public static BitmapSource Grayscale(BitmapSource source)
    {
        // Keep transparency: convert colour channels to luminance but preserve alpha.
        int w = source.PixelWidth, h = source.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        source.CopyPixels(px, stride, 0);
        for (int i = 0; i < px.Length; i += 4)
        {
            byte y = (byte)Math.Round(0.0722 * px[i] + 0.7152 * px[i + 1] + 0.2126 * px[i + 2]);
            px[i] = px[i + 1] = px[i + 2] = y;
        }
        var result = BitmapSource.Create(w, h, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, px, stride);
        result.Freeze();
        return result;
    }
}
