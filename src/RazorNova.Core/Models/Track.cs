namespace RazorNova.Core.Models;

/// <summary>
/// Represents a single audio track in the user's library.
/// This is a plain data model only — no UI logic, no persistence logic.
/// Shared across Metadata, Data, Library, Playlist, Playback, and App.
/// </summary>
public sealed class Track
{
    /// <summary>
    /// Primary key assigned by the database (RazorNova.Data).
    /// Equals 0 for a track that has not been persisted yet.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Absolute path to the audio file on disk. This is the track's
    /// natural unique key and is also used as the lookup key for
    /// album art caching — so a cover image is never mixed up
    /// between two different tracks.
    /// </summary>
    public required string FilePath { get; init; }

    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;

    /// <summary>Total playable length of the track.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// The audio file's last-write timestamp on disk (UTC).
    /// Used for the "sort by file modification date" requirement,
    /// and later to detect if a file changed since the last scan.
    /// </summary>
    public DateTime FileModifiedAtUtc { get; set; }

    /// <summary>
    /// File extension without the dot, upper-invariant (e.g. "MP3", "FLAC").
    /// Derived from FilePath on the fly — never stored separately,
    /// so it can never go out of sync.
    /// </summary>
    public string FileExtension =>
        System.IO.Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();
}