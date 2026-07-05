using RazorNova.Core.Enums;
using RazorNova.Core.Models;

namespace RazorNova.Core.Interfaces;

/// <summary>
/// Persists and queries Track records in SQLite. Implemented by
/// RazorNova.Data. This is the single source of truth for "what's in
/// the library" — both RazorNova.Library (during scanning) and the App's
/// LibraryViewModel (for displaying/searching/sorting) go through this
/// interface rather than touching SQLite directly. Search and sort live
/// here (not on a separate query service) so they can be executed as
/// efficient SQL rather than loading everything into memory first.
/// </summary>
public interface ITrackRepository
{
    /// <summary>
    /// Looks up a track by its file path, used by the folder scanner to
    /// detect whether a file was already scanned before (and if so,
    /// whether FileModifiedAtUtc changed, meaning it should be re-read
    /// and updated rather than inserted as a duplicate). Returns null
    /// if no track with this path exists yet.
    /// </summary>
    Task<Track?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new track and returns the same instance with Id
    /// populated from the database. Throws if a track with the same
    /// FilePath already exists — callers should check GetByFilePathAsync
    /// first and call UpdateAsync instead in that case.
    /// </summary>
    Task<Track> AddAsync(Track track, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts multiple new tracks in a single database transaction.
    /// Used by the folder scanner for efficient bulk inserts instead of
    /// calling AddAsync once per file, which would be far slower for
    /// large libraries. Returns the same instances with Id populated.
    /// </summary>
    Task<IReadOnlyList<Track>> AddManyAsync(IEnumerable<Track> tracks, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing track's fields (matched by Id).</summary>
    /// <exception cref="KeyNotFoundException">No track with this Id exists.</exception>
    Task UpdateAsync(Track track, CancellationToken cancellationToken = default);

    /// <summary>Deletes a track by Id. No-op (does not throw) if it doesn't exist.</summary>
    Task DeleteAsync(int trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlinks an entire scanned folder from the library: deletes every
    /// track whose FilePath is inside <paramref name="folderPath"/>
    /// (including subfolders). Only removes the database records — the
    /// actual files on disk are never touched. This is the "Remove
    /// Folder" / "Unlink Folder" feature, distinct from DeleteAsync which
    /// removes a single track. No-op if no tracks match.
    /// </summary>
    Task DeleteByFolderAsync(string folderPath, CancellationToken cancellationToken = default);

    /// <summary>Returns a single track by Id, or null if not found.</summary>
    Task<Track?> GetByIdAsync(int trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves multiple track Ids at once, preserving the requested
    /// order. Used to turn a PlaylistModel.TrackIds list back into
    /// playable Track objects. Ids that no longer exist (e.g. the file
    /// was removed from the library) are silently skipped rather than
    /// throwing, so a playlist with one stale entry still plays the rest.
    /// </summary>
    Task<IReadOnlyList<Track>> GetByIdsAsync(IEnumerable<int> trackIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every track in the library, sorted by <paramref name="sortBy"/>.
    /// This is the full, unfiltered library list shown when the search box is empty.
    /// </summary>
    Task<IReadOnlyList<Track>> GetAllAsync(SortCriteria sortBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns tracks whose Title, Artist, or Album contains
    /// <paramref name="searchText"/> (case-insensitive), sorted by
    /// <paramref name="sortBy"/>. Designed to be called on every
    /// keystroke for "filter as you type" — implementations should keep
    /// this fast (e.g. an indexed SQL LIKE query) rather than loading
    /// the full table into memory. An empty/whitespace searchText
    /// returns the same result as GetAllAsync.
    /// </summary>
    Task<IReadOnlyList<Track>> SearchAsync(string searchText, SortCriteria sortBy, CancellationToken cancellationToken = default);
}