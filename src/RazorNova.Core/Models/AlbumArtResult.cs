namespace RazorNova.Core.Models;

/// <summary>
/// Represents the resolved album art for a single track, returned by
/// IAlbumArtService. Carries its own cache key so that callers — and the
/// cache implementation itself — can never accidentally associate a cover
/// image with the wrong track. Preventing exactly that mix-up is the
/// entire reason this type exists as its own model instead of a raw byte[].
/// </summary>
public sealed class AlbumArtResult
{
    /// <summary>
    /// Deterministic cache key for this result (see BuildCacheKey below).
    /// Two different tracks can never produce the same key, and if the
    /// underlying file changes, its previous cache entry is naturally
    /// invalidated instead of silently serving stale art.
    /// </summary>
    public required string CacheKey { get; init; }

    /// <summary>
    /// Raw image bytes (JPEG/PNG) for the cover. In practice this is
    /// never null — IAlbumArtService always falls back to the app's
    /// default placeholder cover when no embedded art exists, so
    /// consumers can treat this as always present.
    /// </summary>
    public byte[]? ImageData { get; init; }

    /// <summary>
    /// True if ImageData was extracted from the audio file's own embedded
    /// tag. False means ImageData is the application's default neon-style
    /// placeholder cover.
    /// </summary>
    public bool IsEmbedded { get; init; }

    /// <summary>
    /// Builds the canonical cache key for a track: file path + last-modified
    /// timestamp. If the file's embedded cover is changed externally later,
    /// the timestamp changes too, so the old cached entry is automatically
    /// orphaned instead of being wrongly reused.
    /// </summary>
    public static string BuildCacheKey(string filePath, DateTime fileModifiedAtUtc) =>
        $"{filePath}|{fileModifiedAtUtc.Ticks}";
}