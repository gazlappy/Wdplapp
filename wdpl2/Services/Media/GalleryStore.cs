using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Where the gallery's photos live: as files beside the league, resized for
/// the web, not inside the league file.
/// </summary>
/// <remarks>
/// Each photo is kept twice - a viewing size and a thumbnail - both JPEG, named
/// after the photo's id so a name never collides and never changes. The league
/// file holds only what the photo is (caption, season, file name), so saving
/// it costs the same with one photo or a thousand.
/// <para>
/// The same two folders are what goes up to the website, under /gallery, so
/// the gallery page can link to them rather than carry them.
/// </para>
/// </remarks>
public sealed class GalleryStore
{
    /// <summary>Longest side of the viewing copy, in pixels.</summary>
    public const int FullEdge = 1600;

    /// <summary>Longest side of the thumbnail, in pixels.</summary>
    public const int ThumbEdge = 480;

    public const string FullFolder = "full";
    public const string ThumbFolder = "thumbs";

    /// <summary>Originals waiting to be resized, dropped here from outside the app.</summary>
    public const string IncomingFolder = "incoming";

    public string Root { get; }

    public GalleryStore(string root)
    {
        Root = root;
    }

    /// <summary>The store beside the league the app has open.</summary>
    public static GalleryStore ForLeague() => new(Path.Combine(AppPaths.Data, "gallery"));

    public string FullPath(string stored) => Path.Combine(Root, FullFolder, stored);
    public string ThumbPath(string stored) => Path.Combine(Root, ThumbFolder, stored);

    /// <summary>
    /// Resizes a picture into the store.
    /// </summary>
    /// <returns>The stored file's name, and the viewing copy's size.</returns>
    /// <exception cref="InvalidDataException">It is not a picture that can be read.</exception>
    public (string Stored, int Width, int Height) Import(Stream source, Guid id)
    {
        using var data = SKData.Create(source)
            ?? throw new InvalidDataException("That file could not be read.");
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidDataException("That file is not a picture this app can read.");

        using var decoded = DecodeNear(codec, FullEdge);
        using var upright = Orient(decoded, codec.EncodedOrigin);

        var stored = id.ToString("N") + ".jpg";
        Directory.CreateDirectory(Path.Combine(Root, FullFolder));
        Directory.CreateDirectory(Path.Combine(Root, ThumbFolder));

        var (w, h) = Write(upright, FullEdge, FullPath(stored), quality: 82);
        Write(upright, ThumbEdge, ThumbPath(stored), quality: 75);

        return (stored, w, h);
    }

    /// <summary>Removes a photo's files. Missing files are not an error.</summary>
    public void Delete(string stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return;
        foreach (var path in new[] { FullPath(stored), ThumbPath(stored) })
            if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>
    /// Brings every photo in the gallery into the store.
    /// </summary>
    /// <remarks>
    /// Two kinds are moved: photos still carried inside the league file (as
    /// every photo was before this), and originals left in the "incoming"
    /// folder for a photo that has no stored file yet. Either way the photo is
    /// resized once, and the league file keeps only its name.
    /// <para>
    /// A photo that cannot be read is left exactly as it was and named in the
    /// answer, rather than lost.
    /// </para>
    /// </remarks>
    /// <returns>How many were moved, and the file names of any that could not be.</returns>
    public (int Moved, List<string> Failed) Settle(WebsiteSettings settings)
    {
        var moved = 0;
        var failed = new List<string>();
        var incoming = Path.Combine(Root, IncomingFolder);

        foreach (var image in settings.GalleryImages)
        {
            if (!string.IsNullOrEmpty(image.StoredFile) && File.Exists(FullPath(image.StoredFile)))
            {
                // Already stored; anything still embedded is a leftover copy.
                if (image.ImageData.Length > 0) { image.ImageData = Array.Empty<byte>(); moved++; }
                continue;
            }

            try
            {
                Stream? source = null;
                string? original = null;

                if (image.ImageData.Length > 0)
                {
                    source = new MemoryStream(image.ImageData);
                }
                else if (Directory.Exists(incoming))
                {
                    original = Directory.EnumerateFiles(incoming, image.Id.ToString("N") + ".*").FirstOrDefault();
                    if (original is not null) source = File.OpenRead(original);
                }

                if (source is null) continue;

                using (source)
                {
                    var (stored, w, h) = Import(source, image.Id);
                    image.StoredFile = stored;
                    image.Width = w;
                    image.Height = h;
                    image.ImageData = Array.Empty<byte>();
                }

                if (original is not null) File.Delete(original);
                moved++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GalleryStore: could not store {image.FileName}: {ex.Message}");
                failed.Add(image.FileName);
            }
        }

        if (Directory.Exists(incoming) && !Directory.EnumerateFileSystemEntries(incoming).Any())
            Directory.Delete(incoming);

        return (moved, failed);
    }

    // ------------------------------------------------------------ pictures

    /// <summary>
    /// Decodes at the smallest size that is still at least the target, so a
    /// 12-megapixel phone photo is not unpacked in full just to be shrunk.
    /// JPEG can be decoded at a half, a quarter or an eighth for almost nothing.
    /// </summary>
    private static SKBitmap DecodeNear(SKCodec codec, int edge)
    {
        var info = codec.Info;
        var longest = Math.Max(info.Width, info.Height);
        var scale = longest > edge ? (float)edge / longest : 1f;

        var size = codec.GetScaledDimensions(scale);
        if (Math.Max(size.Width, size.Height) < edge && longest > edge)
            size = codec.GetScaledDimensions(Math.Min(1f, scale * 2f));   // never below the target

        var decodeInfo = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(decodeInfo);
        var result = codec.GetPixels(decodeInfo, bitmap.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
        {
            bitmap.Dispose();
            // Some formats will not decode scaled; take it full size instead.
            bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("That picture could not be decoded.");
        }
        return bitmap;
    }

    /// <summary>
    /// Turns a phone photo the right way up. Cameras save the picture as the
    /// sensor saw it and a note of which way the phone was held; the website
    /// cannot be relied on to read the note, so it is applied here.
    /// </summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                          or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var w = swap ? source.Height : source.Width;
        var h = swap ? source.Width : source.Height;

        var result = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);

        // Photos with see-through parts (a PNG logo, say) become JPEGs: give
        // them white rather than black behind.
        canvas.Clear(SKColors.White);

        // x' = ScaleX*x + SkewX*y + TransX,  y' = SkewY*x + ScaleY*y + TransY
        float fw = w, fh = h;
        var (sx, kx, tx, ky, sy, ty) = origin switch
        {
            SKEncodedOrigin.TopRight    => (-1f, 0f, fw,  0f,  1f, 0f),   // mirrored
            SKEncodedOrigin.BottomRight => (-1f, 0f, fw,  0f, -1f, fh),    // upside down
            SKEncodedOrigin.BottomLeft  => ( 1f, 0f, 0f, 0f, -1f, fh),    // mirrored, upside down
            SKEncodedOrigin.LeftTop     => ( 0f, 1f, 0f, 1f,  0f, 0f),   // transposed
            SKEncodedOrigin.RightTop    => ( 0f,-1f, fw,  1f,  0f, 0f),   // turn a quarter clockwise
            SKEncodedOrigin.RightBottom => ( 0f,-1f, fw, -1f,  0f, fh),    // transverse
            SKEncodedOrigin.LeftBottom  => ( 0f, 1f, 0f,-1f,  0f, fh),    // turn a quarter anticlockwise
            _                           => ( 1f, 0f, 0f, 0f,  1f, 0f),   // already upright
        };
        canvas.SetMatrix(new SKMatrix(sx, kx, tx, ky, sy, ty, 0, 0, 1));

        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    /// <summary>Shrinks to fit the edge (never enlarges) and writes a JPEG.</summary>
    private static (int Width, int Height) Write(SKBitmap source, int edge, string path, int quality)
    {
        var longest = Math.Max(source.Width, source.Height);
        var scale = longest > edge ? (double)edge / longest : 1.0;
        var w = Math.Max(1, (int)Math.Round(source.Width * scale));
        var h = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var sized = scale < 1.0
            ? source.Resize(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul),
                            new SKSamplingOptions(SKCubicResampler.Mitchell))
            : source.Copy();
        using var image = SKImage.FromBitmap(sized);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        var temp = path + ".writing";
        using (var file = File.Create(temp))
            jpeg.SaveTo(file);
        File.Move(temp, path, overwrite: true);

        return (w, h);
    }
}
