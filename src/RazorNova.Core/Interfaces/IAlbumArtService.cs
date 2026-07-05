using RazorNova.Core.Models;

namespace RazorNova.Core.Interfaces;

/// <summary>
/// Resolves album art for a track — either extracted from the file's
/// embedded tag, or the app's default neon-style placeholder cover when
/// none exists. Implemented by RazorNova.Metadata, with an in-memory
/// cache keyed by AlbumArtResult.BuildCacheKey so covers are never
/// re-extracted unnecessarily and never mixed up between tracks.
/// </summary>
public interface IAlbumArtService
{
    /// <summary>
    /// Resolves the album art for <paramref name="track"/>. Checks the
    /// cache first using the track's file path + last-modified timestamp;
    /// on a miss, extracts the embedded cover via TagLib# (or falls back
    /// to the default placeholder), caches the result, and returns it.
    /// Runs file I/O off the UI thread.
    /// </summary>
    Task<AlbumArtResult> GetAlbumArtAsync(Track track, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the application's default placeholder cover (abstract
    /// neon style matching the Midnight Purple theme) without touching
    /// any audio file. Used directly by the UI when no track is loaded
    /// at all (e.g. on app startup before anything plays).
    /// </summary>
    AlbumArtResult GetDefaultCover();

    /// <summary>
    /// Removes a specific track's entry from the in-memory cache, if
    /// present. Useful after editing a file's tags externally (relevant
    /// once the v1.2 Tag Editor exists) so stale art isn't served from cache.
    /// </summary>
    void InvalidateCache(string filePath);
}