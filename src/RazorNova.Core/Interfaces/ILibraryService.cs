using RazorNova.Core.Models;

namespace RazorNova.Core.Interfaces;

/// <summary>
/// Orchestrates scanning a folder for supported audio files, reading their
/// metadata, and persisting them into the library database. Implemented by
/// RazorNova.Library, which coordinates IMetadataReader (to read tags from
/// each file) and ITrackRepository (to persist the results) — without
/// either of those two projects ever referencing each other directly.
/// </summary>
public interface ILibraryService
{
    /// <summary>
    /// Recursively scans <paramref name="folderPath"/> and all subfolders
    /// for supported audio files (.mp3, .flac, .wav, .m4a), reads each
    /// file's metadata via IMetadataReader, persists new or changed tracks
    /// via ITrackRepository, and returns a final summary. Runs entirely
    /// off the UI thread so the app never freezes during a large scan.
    /// </summary>
    /// <param name="progress">
    /// Optional live progress reporter, raised as each file finishes
    /// processing. Drives the scan dialog's running "X files found,
    /// Y total duration" display while the scan is still in progress.
    /// </param>
    /// <param name="cancellationToken">
    /// Allows the user to cancel a long-running scan (e.g. closing the dialog).
    /// </param>
    Task<ScanResult> ScanFolderAsync(
        string folderPath,
        IProgress<(int FilesFoundSoFar, TimeSpan TotalDurationSoFar)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one or more explicitly chosen audio files — the manual,
    /// multi-file-select counterpart to ScanFolderAsync. Reuses the exact
    /// same per-file metadata-read/persist logic internally. Unlike
    /// ScanFolderAsync, a file with an unsupported extension or that no
    /// longer exists on disk counts as a failure here rather than being
    /// silently skipped, since the caller explicitly chose these files.
    /// Cancellation is all-or-nothing, same as ScanFolderAsync: nothing is
    /// persisted if the operation is cancelled midway through.
    /// </summary>
    /// <param name="filePaths">The explicit file paths to add.</param>
    /// <param name="progress">
    /// Optional live progress reporter, same shape as ScanFolderAsync's.
    /// </param>
    /// <param name="cancellationToken">Allows cancelling the operation.</param>
    Task<AddFilesResult> AddFilesAsync(
        IEnumerable<string> filePaths,
        IProgress<(int FilesFoundSoFar, TimeSpan TotalDurationSoFar)>? progress = null,
        CancellationToken cancellationToken = default);
}