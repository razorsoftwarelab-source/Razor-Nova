using RazorNova.Core.Models;

namespace RazorNova.Core.Interfaces;

/// <summary>
/// Persists and retrieves named playlists (PlaylistModel) in SQLite.
/// Implemented by RazorNova.Data. Deliberately separate from
/// ITrackRepository even though both live in the same Data project —
/// playlists and tracks are different concerns with different lifetimes
/// (deleting a playlist must never delete the tracks it references).
/// </summary>
public interface IPlaylistRepository
{
    /// <summary>Returns every saved playlist, ordered by CreatedAtUtc (oldest first).</summary>
    Task<IReadOnlyList<PlaylistModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a single playlist by Id, or null if not found.</summary>
    Task<PlaylistModel?> GetByIdAsync(int playlistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new playlist and returns the same instance with Id
    /// populated from the database.
    /// </summary>
    Task<PlaylistModel> AddAsync(PlaylistModel playlist, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing playlist's Name and/or TrackIds (matched by Id) —
    /// e.g. after the user renames a playlist or reorders/adds/removes
    /// tracks within it.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No playlist with this Id exists.</exception>
    Task UpdateAsync(PlaylistModel playlist, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a playlist by Id. Only removes the playlist record itself
    /// (its Name and TrackIds list) — the underlying tracks in the
    /// library are never touched. No-op (does not throw) if the playlist doesn't exist.
    /// </summary>
    Task DeleteAsync(int playlistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One‑time housekeeping: fixes any playlist whose <see cref="PlaylistModel.Name"/>
    /// has been corrupted (e.g. contains the full class name instead of a real name).
    /// Renames them to "Playlist (Renamed)" so the UI and future operations work correctly.
    /// </summary>
    Task CleanCorruptedPlaylistNamesAsync(CancellationToken cancellationToken = default);
}