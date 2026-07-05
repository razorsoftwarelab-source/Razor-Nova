using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;

namespace RazorNova.Library;

/// <summary>
/// Implements ILibraryService: ScanFolderAsync recursively walks a folder,
/// AddFilesAsync takes an explicit file list — both funnel through the same
/// private ReadOrReuseTrackAsync helper for the actual per-file metadata
/// read/persist logic, so that logic exists in exactly one place. Asks
/// IMetadataReader.IsSupportedExtension to cheaply filter out non-audio
/// files, reads metadata only for files that are new or have changed since
/// they were last seen, and persists results via ITrackRepository. Receives
/// both dependencies purely as Core interfaces through the constructor —
/// this project has no reference to RazorNova.Metadata or RazorNova.Data;
/// the App project wires the concrete implementations in.
/// </summary>
public sealed class FolderScannerService : ILibraryService
{
    private readonly IMetadataReader _metadataReader;
    private readonly ITrackRepository _trackRepository;

    public FolderScannerService(IMetadataReader metadataReader, ITrackRepository trackRepository)
    {
        _metadataReader = metadataReader;
        _trackRepository = trackRepository;
    }

    public async Task<ScanResult> ScanFolderAsync(
        string folderPath,
        IProgress<(int FilesFoundSoFar, TimeSpan TotalDurationSoFar)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

        var resultTracks = new List<Track>();
        var newTracks = new List<Track>();
        var changedExistingTracks = new List<Track>();
        var failedCount = 0;
        var foundSoFar = 0;
        var durationSoFar = TimeSpan.Zero;

        foreach (var filePath in EnumerateAllFiles(folderPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Cheap extension check first — never opens or parses files
            // that aren't one of our supported audio formats. A folder scan
            // walks everything on disk, so an unsupported file here is
            // normal and expected — silently skipped, not a failure.
            if (!_metadataReader.IsSupportedExtension(filePath))
                continue;

            var resolvedTrack = await ReadOrReuseTrackAsync(
                filePath, newTracks, changedExistingTracks, cancellationToken).ConfigureAwait(false);

            if (resolvedTrack is null)
            {
                failedCount++;
                continue;
            }

            // resultTracks holds the SAME Track instances added to
            // newTracks/changedExistingTracks above (Track is a reference
            // type). AddManyAsync/UpdateAsync below populate each
            // instance's Id in place, so these entries automatically end
            // up with correct, final Ids once persistence finishes.
            resultTracks.Add(resolvedTrack);
            foundSoFar++;
            durationSoFar += resolvedTrack.Duration;
            progress?.Report((foundSoFar, durationSoFar));
        }

        await PersistAsync(newTracks, changedExistingTracks, cancellationToken).ConfigureAwait(false);

        return new ScanResult
        {
            FolderPath = folderPath,
            Tracks = resultTracks,
            FailedFilesCount = failedCount
        };
    }

    public async Task<AddFilesResult> AddFilesAsync(
        IEnumerable<string> filePaths,
        IProgress<(int FilesFoundSoFar, TimeSpan TotalDurationSoFar)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var resultTracks = new List<Track>();
        var newTracks = new List<Track>();
        var changedExistingTracks = new List<Track>();
        var failedCount = 0;
        var foundSoFar = 0;
        var durationSoFar = TimeSpan.Zero;

        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Unlike ScanFolderAsync, these are files the caller explicitly
            // chose one at a time in a dialog — an unsupported extension or
            // a file that's since vanished from disk is a real failure to
            // report, not something to quietly drop.
            if (!File.Exists(filePath) || !_metadataReader.IsSupportedExtension(filePath))
            {
                failedCount++;
                continue;
            }

            var resolvedTrack = await ReadOrReuseTrackAsync(
                filePath, newTracks, changedExistingTracks, cancellationToken).ConfigureAwait(false);

            if (resolvedTrack is null)
            {
                failedCount++;
                continue;
            }

            resultTracks.Add(resolvedTrack);
            foundSoFar++;
            durationSoFar += resolvedTrack.Duration;
            progress?.Report((foundSoFar, durationSoFar));
        }

        await PersistAsync(newTracks, changedExistingTracks, cancellationToken).ConfigureAwait(false);

        return new AddFilesResult
        {
            Tracks = resultTracks,
            FailedFilesCount = failedCount
        };
    }

    /// <summary>
    /// Resolves a single file to a Track: reuses the existing database row
    /// unchanged if its on-disk last-write time still matches (skipping an
    /// expensive tag re-read), otherwise reads fresh metadata and queues the
    /// result into whichever of newTracks/changedExistingTracks applies.
    /// Returns null if the file is corrupt, locked, or otherwise unreadable
    /// — callers treat null as "this file failed" and count it accordingly.
    /// Shared by both ScanFolderAsync and AddFilesAsync so this logic exists
    /// in exactly one place.
    /// </summary>
    private async Task<Track?> ReadOrReuseTrackAsync(
        string filePath,
        List<Track> newTracks,
        List<Track> changedExistingTracks,
        CancellationToken cancellationToken)
    {
        try
        {
            var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
            var existing = await _trackRepository.GetByFilePathAsync(filePath, cancellationToken).ConfigureAwait(false);

            if (existing is not null && existing.FileModifiedAtUtc == lastWriteUtc)
            {
                // Already in the library and unchanged on disk since it was
                // last seen — skip the expensive tag re-read entirely.
                return existing;
            }

            var resolvedTrack = await _metadataReader.ReadAsync(filePath, cancellationToken).ConfigureAwait(false);

            if (existing is not null)
            {
                // File changed (re-tagged, replaced) since it was last
                // seen — keep its existing database Id on update.
                resolvedTrack.Id = existing.Id;
                changedExistingTracks.Add(resolvedTrack);
            }
            else
            {
                newTracks.Add(resolvedTrack);
            }

            return resolvedTrack;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Corrupt, locked, or unreadable file — the caller counts this
            // and moves on; one bad file must never abort the whole operation.
            return null;
        }
    }

    private async Task PersistAsync(
        List<Track> newTracks,
        List<Track> changedExistingTracks,
        CancellationToken cancellationToken)
    {
        if (newTracks.Count > 0)
            await _trackRepository.AddManyAsync(newTracks, cancellationToken).ConfigureAwait(false);

        foreach (var changedTrack in changedExistingTracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _trackRepository.UpdateAsync(changedTrack, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> EnumerateAllFiles(string folderPath)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };
        return Directory.EnumerateFiles(folderPath, "*", options);
    }
}