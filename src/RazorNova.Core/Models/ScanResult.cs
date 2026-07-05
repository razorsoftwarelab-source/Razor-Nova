namespace RazorNova.Core.Models;

/// <summary>
/// Summarizes the outcome of a folder scan performed by ILibraryService.
/// This is exactly what the Folder Scan dialog displays to the user:
/// how many audio files were found and their combined total duration.
/// </summary>
public sealed class ScanResult
{
    /// <summary>The root folder path that was scanned (scan is recursive into subfolders).</summary>
    public required string FolderPath { get; init; }

    /// <summary>
    /// Tracks successfully read and ready to be persisted to the library.
    /// Each Track here already has its metadata (Title/Artist/Album/Duration)
    /// populated by IMetadataReader during the scan.
    /// </summary>
    public List<Track> Tracks { get; init; } = new();

    /// <summary>Number of supported audio files found and successfully read. Equals Tracks.Count.</summary>
    public int FilesFoundCount => Tracks.Count;

    /// <summary>Combined playable duration of every track found (e.g. shown as "3h 42m" in the UI).</summary>
    public TimeSpan TotalDuration => Tracks.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Duration);

    /// <summary>
    /// Files that matched a supported extension (mp3/flac/wav/m4a) but
    /// failed to read — corrupt file, locked, bad tag data, etc. Reported
    /// to the user as "X files could not be read" instead of failing the whole scan.
    /// </summary>
    public int FailedFilesCount { get; init; }

    public DateTime ScanCompletedAtUtc { get; init; } = DateTime.UtcNow;
}