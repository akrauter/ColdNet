using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SysEncoder = System.Drawing.Imaging.Encoder;

namespace ColdNet.Modules.GraphicsConversion;

/// <summary>
/// Multi-page TIFF read/write and raw-pixel helpers built on <c>System.Drawing.Common</c> (MIT
/// licensed, GDI+-backed, Windows-only) - used instead of a third-party imaging library so the
/// graphics-conversion and barcode-splitting modules only depend on MIT-licensed packages.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class GdiTiff
{
    /// <summary>Reads every frame of a (possibly multi-page) TIFF as standalone, disposable bitmaps.</summary>
    public static List<Bitmap> ReadFrames(string path)
    {
        using var source = Image.FromFile(path);
        var dimension = new FrameDimension(source.FrameDimensionsList[0]);
        var frameCount = source.GetFrameCount(dimension);

        var frames = new List<Bitmap>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            source.SelectActiveFrame(dimension, i);

            var frame = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(frame))
            {
                g.DrawImage(source, Point.Empty);
            }

            frames.Add(frame);
        }

        return frames;
    }

    /// <summary>Writes one or more frames as a single (multi-page, if more than one) TIFF file.</summary>
    public static void WriteFrames(string path, IReadOnlyList<Bitmap> frames)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        }

        if (frames.Count == 1)
        {
            frames[0].Save(path, ImageFormat.Tiff);
            return;
        }

        var encoderInfo = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Tiff.Guid);

        using var saveParams = new EncoderParameters(2);
        saveParams.Param[0] = new EncoderParameter(SysEncoder.Compression, (long)EncoderValue.CompressionLZW);
        saveParams.Param[1] = new EncoderParameter(SysEncoder.SaveFlag, (long)EncoderValue.MultiFrame);
        frames[0].Save(path, encoderInfo, saveParams);

        for (var i = 1; i < frames.Count; i++)
        {
            using var frameParams = new EncoderParameters(1);
            frameParams.Param[0] = new EncoderParameter(SysEncoder.SaveFlag, (long)EncoderValue.FrameDimensionPage);
            frames[0].SaveAdd(frames[i], frameParams);
        }

        using var flushParams = new EncoderParameters(1);
        flushParams.Param[0] = new EncoderParameter(SysEncoder.SaveFlag, (long)EncoderValue.Flush);
        frames[0].SaveAdd(flushParams);
    }

    /// <summary>
    /// Raw BGRA32 pixel bytes of <paramref name="image"/>, normalized to 32bpp first so the
    /// stride is always exactly <c>width * 4</c> - matches ZXing.Net's <c>BitmapFormat.BGRA32</c>.
    /// </summary>
    public static byte[] GetBgra32Bytes(Image image)
    {
        using var normalized = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(normalized))
        {
            g.DrawImageUnscaled(image, 0, 0);
        }

        var rect = new Rectangle(0, 0, normalized.Width, normalized.Height);
        var data = normalized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * normalized.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            normalized.UnlockBits(data);
        }
    }
}
