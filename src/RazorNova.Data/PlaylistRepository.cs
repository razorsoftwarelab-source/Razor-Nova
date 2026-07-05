using Microsoft.Data.Sqlite;
using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;

namespace RazorNova.Data;

/// <summary>
/// SQLite-based implementation of IPlaylistRepository. Stores
/// PlaylistModel.TrackIds as a comma-separated string in the TrackIdsCsv
/// column rather than a separate junction table — sufficient for MVP-scale
/// playlists (hundreds of tracks) and far simpler to read/write atomically
/// as a single ordered list. Deleting a playlist only ever removes its own
/// row; it never touches the Tracks table.
/// </summary>
public sealed class PlaylistRepository : IPlaylistRepository
{
    private readonly DatabaseContext _db;

    public PlaylistRepository(DatabaseContext db) => _db = db;

    public async Task<IReadOnlyList<PlaylistModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, TrackIdsCsv, CreatedAtTicks
            FROM Playlists
            ORDER BY CreatedAtTicks ASC;
            """;

        var results = new List<PlaylistModel>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(MapPlaylist(reader));
        return results;
    }

    public async Task<PlaylistModel?> GetByIdAsync(int playlistId, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, TrackIdsCsv, CreatedAtTicks
            FROM Playlists WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", playlistId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapPlaylist(reader) : null;
    }

    public async Task<PlaylistModel> AddAsync(PlaylistModel playlist, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = """
                INSERT INTO Playlists (Name, TrackIdsCsv, CreatedAtTicks)
                VALUES ($name, $trackIds, $created);
                """;
            insertCommand.Parameters.AddWithValue("$name", playlist.Name);
            insertCommand.Parameters.AddWithValue("$trackIds", SerializeTrackIds(playlist.TrackIds));
            insertCommand.Parameters.AddWithValue("$created", playlist.CreatedAtUtc.Ticks);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)(await idCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        playlist.Id = (int)newId;
        return playlist;
    }

    public async Task UpdateAsync(PlaylistModel playlist, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Playlists
            SET Name = $name, TrackIdsCsv = $trackIds
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$name", playlist.Name);
        command.Parameters.AddWithValue("$trackIds", SerializeTrackIds(playlist.TrackIds));
        command.Parameters.AddWithValue("$id", playlist.Id);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rowsAffected == 0)
            throw new KeyNotFoundException($"No playlist exists with Id {playlist.Id}.");
    }

    public async Task DeleteAsync(int playlistId, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Playlists WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", playlistId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    //  NEW: one‑time cleanup for corrupted playlist names (issue #1)
    // -----------------------------------------------------------------------
    public async Task CleanCorruptedPlaylistNamesAsync(CancellationToken cancellationToken = default)
    {
        const string marker = "RazorNova.Core.Models.PlaylistModel";
        const string replacementName = "Playlist (Renamed)";

        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Playlists
            SET Name = $replacementName
            WHERE Name LIKE $marker;
            """;
        command.Parameters.AddWithValue("$replacementName", replacementName);
        command.Parameters.AddWithValue("$marker", "%" + marker + "%");
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string SerializeTrackIds(List<int> trackIds) =>
        string.Join(",", trackIds);

    private static List<int> DeserializeTrackIds(string csv) =>
        string.IsNullOrEmpty(csv)
            ? new List<int>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                 .Select(int.Parse)
                 .ToList();

    private static PlaylistModel MapPlaylist(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        TrackIds = DeserializeTrackIds(reader.GetString(2)),
        CreatedAtUtc = new DateTime(reader.GetInt64(3), DateTimeKind.Utc)
    };
}