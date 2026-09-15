using ImageMagick;

namespace ColdNet.Modules.GraphicsConversion;

/// <summary>
/// Multi-page TIFF read/write and pixel helpers built on <c>Magick.NET</c> (Apache-2.0, cross-platform / Linux Docker compatible).
/// </summary>
public static class MagickTiff
{
    /// <summary>Reads every frame of a (possibly multi-page) TIFF as a collection of Magick images.</summary>
    public static MagickImageCollection ReadFrames(string path)
    {
        var collection = new MagickImageCollection();
        collection.Read(path);
        return collection;
    }

    /// <summary>Writes one or more frames as a single (multi-page, if more than one) TIFF file.</summary>
    public static void WriteFrames(string path, IEnumerable<IMagickImage<byte>> frames)
    {
        using var collection = new MagickImageCollection();
        foreach (var frame in frames)
        {
            var clone = frame.Clone();
            clone.Format = MagickFormat.Tiff;
            clone.Settings.Compression = CompressionMethod.LZW;
            collection.Add(clone);
        }

        if (collection.Count == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        }

        collection.Write(path, MagickFormat.Tiff);
    }

    /// <summary>
    /// Raw BGRA32 pixel bytes of <paramref name="image"/> matching ZXing.Net's <c>BitmapFormat.BGRA32</c>.
    /// </summary>
    public static byte[] GetBgra32Bytes(IMagickImage<byte> image)
    {
        using var clone = image.Clone();
        clone.ColorSpace = ColorSpace.sRGB;
        return clone.ToByteArray(MagickFormat.Bgra);
    }
}
