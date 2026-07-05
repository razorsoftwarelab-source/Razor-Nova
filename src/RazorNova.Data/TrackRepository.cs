using Microsoft.Data.Sqlite;
using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;

namespace RazorNova.Data;

/// <summary>
/// SQLite-based implementation of ITrackRepository. Opens a short-lived
/// connection per operation via DatabaseContext rather than holding one
/// connection open for the repository's lifetime — SQLite connections are
/// cheap to open, and this keeps concurrent reads/writes (e.g. a folder
/// scan inserting tracks while the library list is being queried) safe
/// under WAL mode without manual connection-sharing logic.
/// </summary>
public sealed class TrackRepository : ITrackRepository
{
    private readonly DatabaseContext _db;

    public TrackRepository(DatabaseContext db) => _db = db;

    public async Task<Track?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, FilePath, Title, Artist, Album, DurationTicks, FileModifiedTicks
            FROM Tracks WHERE FilePath = $filePath;
            """;
        command.Parameters.AddWithValue("$filePath", filePath);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapTrack(reader) : null;
    }

    public async Task<Track> AddAsync(Track track, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = """
                INSERT INTO Tracks (FilePath, Title, Artist, Album, DurationTicks, FileModifiedTicks)
                VALUES ($filePath, $title, $artist, $album, $duration, $modified);
                """;
            AddTrackParameters(insertCommand, track);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // last_insert_rowid() is scoped to *this* connection handle — must
        // run on the same `connection` object used for the INSERT above.
        using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)(await idCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        track.Id = (int)newId;
        return track;
    }

    public async Task<IReadOnlyList<Track>> AddManyAsync(IEnumerable<Track> tracks, CancellationToken cancellationToken = default)
    {
        var trackList = tracks.ToList();
        if (trackList.Count == 0) return Array.Empty<Track>();

        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO Tracks (FilePath, Title, Artist, Album, DurationTicks, FileModifiedTicks)
            VALUES ($filePath, $title, $artist, $album, $duration, $modified);
            """;

        using var idCommand = connection.CreateCommand();
        idCommand.Transaction = transaction;
        idCommand.CommandText = "SELECT last_insert_rowid();";

        foreach (var track in trackList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            insertCommand.Parameters.Clear();
            AddTrackParameters(insertCommand, track);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            var newId = (long)(await idCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            track.Id = (int)newId;
        }

        transaction.Commit();
        return trackList;
    }

    public async Task UpdateAsync(Track track, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Tracks
            SET FilePath = $filePath, Title = $title, Artist = $artist, Album = $album,
                DurationTicks = $duration, FileModifiedTicks = $modified
            WHERE Id = $id;
            """;
        AddTrackParameters(command, track);
        command.Parameters.AddWithValue("$id", track.Id);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rowsAffected == 0)
            throw new KeyNotFoundException($"No track exists with Id {track.Id}.");
    }

    public async Task DeleteAsync(int trackId, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tracks WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", trackId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteByFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        // Trim any trailing separator the caller might have included (e.g.
        // from a folder picker), then require a literal "\" right after
        // the folder path in the match. Without that separator, a plain
        // "LIKE 'D:\Music%'" would WRONGLY also match "D:\Music2\song.mp3"
        // — an unrelated sibling folder that merely starts with the same
        // characters. The trailing "\%" closes that gap.
        var normalizedFolder = folderPath.TrimEnd('\\', '/');
        var pattern = EscapeLikePattern(normalizedFolder) + "\\%";

        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        // '~' (rather than the more common '\') is the escape character
        // here specifically because '\' is the Windows path separator and
        // appears constantly in FilePath values — using it as the escape
        // char would require escaping every single path separator too.
        command.CommandText = "DELETE FROM Tracks WHERE FilePath LIKE $pattern ESCAPE '~';";
        command.Parameters.AddWithValue("$pattern", pattern);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Track?> GetByIdAsync(int trackId, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, FilePath, Title, Artist, Album, DurationTicks, FileModifiedTicks
            FROM Tracks WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", trackId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapTrack(reader) : null;
    }

    public async Task<IReadOnlyList<Track>> GetByIdsAsync(IEnumerable<int> trackIds, CancellationToken cancellationToken = default)
    {
        var idList = trackIds.ToList();
        if (idList.Count == 0) return Array.Empty<Track>();

        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();

        // Each id gets its own parameter (never string-concatenated into
        // the SQL text) so this stays injection-safe regardless of input.
        var parameterNames = idList.Select((_, i) => $"$id{i}").ToList();
        command.CommandText = $"""
            SELECT Id, FilePath, Title, Artist, Album, DurationTicks, FileModifiedTicks
            FROM Tracks WHERE Id IN ({string.Join(",", parameterNames)});
            """;
        for (var i = 0; i < idList.Count; i++)
            command.Parameters.AddWithValue(parameterNames[i], idList[i]);

        var byId = new Dictionary<int, Track>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var track = MapTrack(reader);
            byId[track.Id] = track;
        }

        // Preserve the caller's requested order; silently skip ids that no longer exist.
        return idList.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    public Task<IReadOnlyList<Track>> GetAllAsync(SortCriteria sortBy, CancellationToken cancellationToken = default) =>
        QueryTracksAsync(whereClause: null, parameterValue: null, sortBy, cancellationToken);

    public Task<IReadOnlyList<Track>> SearchAsync(string searchText, SortCriteria sortBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return GetAllAsync(sortBy, cancellationToken);

        const string whereClause = "WHERE Title LIKE $pattern COLLATE NOCASE OR Artist LIKE $pattern COLLATE NOCASE OR Album LIKE $pattern COLLATE NOCASE";
        return QueryTracksAsync(whereClause, $"%{searchText}%", sortBy, cancellationToken);
    }

    private async Task<IReadOnlyList<Track>> QueryTracksAsync(
        string? whereClause, string? parameterValue, SortCriteria sortBy, CancellationToken cancellationToken)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Id, FilePath, Title, Artist, Album, DurationTicks, FileModifiedTicks
            FROM Tracks
            {whereClause}
            ORDER BY {OrderByClause(sortBy)};
            """;
        if (parameterValue is not null)
            command.Parameters.AddWithValue("$pattern", parameterValue);

        var results = new List<Track>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(MapTrack(reader));
        return results;
    }

    private static string OrderByClause(SortCriteria sortBy) => sortBy switch
    {
        SortCriteria.Name => "Title COLLATE NOCASE ASC",
        SortCriteria.ModifiedDate => "FileModifiedTicks DESC",
        _ => "Title COLLATE NOCASE ASC"
    };

    /// <summary>
    /// Escapes the three characters that are special inside a SQL LIKE
    /// pattern ('~' itself, '%', '_') so a folder path containing them
    /// literally (e.g. "C:\Users\some_folder") is matched as literal text
    /// rather than as wildcards. Must run before the trailing "\%" is
    /// appended in DeleteByFolderAsync, since that final '%' is meant to
    /// stay a real wildcard.
    /// </summary>
    private static string EscapeLikePattern(string value) =>
        value.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_");

    private static void AddTrackParameters(SqliteCommand command, Track track)
    {
        command.Parameters.AddWithValue("$filePath", track.FilePath);
        command.Parameters.AddWithValue("$title", track.Title);
        command.Parameters.AddWithValue("$artist", track.Artist);
        command.Parameters.AddWithValue("$album", track.Album);
        command.Parameters.AddWithValue("$duration", track.Duration.Ticks);
        command.Parameters.AddWithValue("$modified", track.FileModifiedAtUtc.Ticks);
    }

    private static Track MapTrack(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        FilePath = reader.GetString(1),
        Title = reader.GetString(2),
        Artist = reader.GetString(3),
        Album = reader.GetString(4),
        Duration = TimeSpan.FromTicks(reader.GetInt64(5)),
        FileModifiedAtUtc = new DateTime(reader.GetInt64(6), DateTimeKind.Utc)
    };
}