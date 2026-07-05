namespace RazorNova.Core.Models;

/// <summary>
/// Represents a named, persisted playlist: an ordered collection of tracks.
/// This is a plain data model only. The live playback queue (current/shuffle
/// order, repeat mode) is handled at runtime by IPlaylistManager — this
/// model is what IPlaylistRepository saves to and loads from SQLite.
/// </summary>
public sealed class PlaylistModel
{
    /// <summary>
    /// Primary key assigned by the database. Equals 0 for a playlist
    /// that has not been persisted yet.
    /// </summary>
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of Track.Id values belonging to this playlist.
    /// The order of this list IS the playlist order (index 0 plays first).
    /// We store IDs rather than full Track objects on purpose: it keeps
    /// this model lightweight, avoids duplicating track data, and avoids
    /// staleness if a track's metadata changes after being added here.
    /// Callers resolve the actual Track objects via ITrackRepository
    /// when they need to play or display the playlist.
    /// </summary>
    public List<int> TrackIds { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; }
}