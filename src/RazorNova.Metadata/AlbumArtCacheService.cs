using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;
using TagLibFile = TagLib.File;

namespace RazorNova.Metadata;

/// <summary>
/// TagLib#-based implementation of IAlbumArtService. Extracts embedded
/// cover art from audio files, falls back to a procedurally generated
/// neon-style placeholder when none exists, and caches results in memory
/// keyed by AlbumArtResult.BuildCacheKey (file path + modified timestamp)
/// so covers are never re-extracted unnecessarily and never mixed up
/// between tracks.
/// </summary>
public sealed class AlbumArtCacheService : IAlbumArtService
{
    private readonly ConcurrentDictionary<string, AlbumArtResult> _cache = new();
    private readonly Lazy<AlbumArtResult> _defaultCover = new(BuildDefaultCover);

    public async Task<AlbumArtResult> GetAlbumArtAsync(Track track, CancellationToken cancellationToken = default)
    {
        var cacheKey = AlbumArtResult.BuildCacheKey(track.FilePath, track.FileModifiedAtUtc);

        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var result = await Task.Run(
            () => ExtractEmbeddedArt(track.FilePath, cacheKey, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        _cache[cacheKey] = result;
        return result;
    }

    public AlbumArtResult GetDefaultCover() => _defaultCover.Value;

    public void InvalidateCache(string filePath)
    {
        // Cache keys are "{filePath}|{ticks}". A file can have multiple
        // stale entries from earlier timestamps, so remove all matches
        // for this path rather than assuming a single current key.
        var prefix = filePath + "|";
        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                _cache.TryRemove(key, out _);
        }
    }

    private AlbumArtResult ExtractEmbeddedArt(string filePath, string cacheKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var tagFile = TagLibFile.Create(filePath);
            var pictures = tagFile.Tag.Pictures;

            if (pictures is { Length: > 0 } && pictures[0]?.Data?.Data is { Length: > 0 } bytes)
            {
                return new AlbumArtResult
                {
                    CacheKey = cacheKey,
                    ImageData = bytes,
                    IsEmbedded = true
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Corrupt/locked file or unreadable tag data — album art is a
            // "nice to have"; a bad cover must never break playback or scanning.
            // Falls through to the default cover below.
        }

        return GetDefaultCover();
    }

    /// <summary>
    /// Procedurally draws the app's default placeholder cover: a matte
    /// black (#0D0D0D) background with a neon orange (#FF8800) radial
    /// glow, faint concentric "sound wave" rings, and a simple music-note
    /// silhouette — generated once and cached for the lifetime of the app.
    /// </summary>
    private static AlbumArtResult BuildDefaultCover()
    {
        const int size = 600;

        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ColorTranslator.FromHtml("#0D0D0D"));

        var center = new PointF(size / 2f, size / 2f);

        using var glowPath = new GraphicsPath();
        glowPath.AddEllipse(center.X - size * 0.42f, center.Y - size * 0.42f, size * 0.84f, size * 0.84f);
        using var glowBrush = new PathGradientBrush(glowPath)
        {
            CenterColor = ColorTranslator.FromHtml("#FF8800"),
            SurroundColors = new[] { ColorTranslator.FromHtml("#0D0D0D") }
        };
        g.FillPath(glowBrush, glowPath);

        using var ringPen = new Pen(Color.FromArgb(90, 255, 136, 0), 3f);
        for (var i = 1; i <= 3; i++)
        {
            var radius = size * (0.20f + i * 0.10f);
            g.DrawEllipse(ringPen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        }

        DrawMusicNote(g, center, size * 0.30f);

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        return new AlbumArtResult
        {
            CacheKey = "::default-cover::",
            ImageData = stream.ToArray(),
            IsEmbedded = false
        };
    }

    private static void DrawMusicNote(Graphics g, PointF center, float scale)
    {
        using var noteBrush = new SolidBrush(ColorTranslator.FromHtml("#0D0D0D"));

        var noteHeadRadius = scale * 0.22f;
        var stemHeight = scale * 1.1f;
        var stemX = center.X + scale * 0.18f;

        g.FillEllipse(noteBrush,
            center.X - noteHeadRadius - scale * 0.05f,
            center.Y + stemHeight * 0.30f - noteHeadRadius,
            noteHeadRadius * 2,
            noteHeadRadius * 1.6f);

        using var stemPen = new Pen(noteBrush, scale * 0.10f);
        g.DrawLine(stemPen, stemX, center.Y - stemHeight * 0.65f, stemX, center.Y + stemHeight * 0.35f);

        var flag = new[]
        {
            new PointF(stemX, center.Y - stemHeight * 0.65f),
            new PointF(stemX + scale * 0.45f, center.Y - stemHeight * 0.40f),
            new PointF(stemX + scale * 0.30f, center.Y - stemHeight * 0.10f),
            new PointF(stemX, center.Y - stemHeight * 0.25f)
        };
        g.FillPolygon(noteBrush, flag);
    }
}