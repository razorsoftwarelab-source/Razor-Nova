using Microsoft.Data.Sqlite;
using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;

namespace RazorNova.Data;

/// <summary>
/// SQLite-based implementation of ISettingsService. Persists the single
/// AppSettings row — the schema's CHECK (Id = 1) constraint on AppSettings
/// (see DatabaseContext) guarantees there is never more than one row, so
/// SaveAsync always upserts into that one fixed row rather than inserting duplicates.
/// </summary>
public sealed class SettingsRepository : ISettingsService
{
    private readonly DatabaseContext _db;

    public SettingsRepository(DatabaseContext db) => _db = db;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT VolumePercent, IsMuted, Theme, LanguageCode, Repeat, IsShuffleEnabled, LastScannedFolderPath
            FROM AppSettings WHERE Id = 1;
            """;

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        // First run — no row saved yet. Return the documented defaults
        // from AppSettings itself rather than throwing or returning null.
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return new AppSettings();

        return new AppSettings
        {
            VolumePercent = reader.GetInt32(0),
            IsMuted = reader.GetInt32(1) != 0,
            Theme = Enum.Parse<ThemeMode>(reader.GetString(2)),
            LanguageCode = reader.IsDBNull(3) ? null : reader.GetString(3),
            Repeat = Enum.Parse<RepeatMode>(reader.GetString(4)),
            IsShuffleEnabled = reader.GetInt32(5) != 0,
            LastScannedFolderPath = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        using var connection = await _db.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();

        // INSERT ... ON CONFLICT DO UPDATE: works whether this is the very
        // first save (no row yet → inserts) or every save after that
        // (row exists → updates in place). Id is always 1.
        command.CommandText = """
            INSERT INTO AppSettings (Id, VolumePercent, IsMuted, Theme, LanguageCode, Repeat, IsShuffleEnabled, LastScannedFolderPath)
            VALUES (1, $volume, $muted, $theme, $language, $repeat, $shuffle, $lastFolder)
            ON CONFLICT (Id) DO UPDATE SET
                VolumePercent = excluded.VolumePercent,
                IsMuted = excluded.IsMuted,
                Theme = excluded.Theme,
                LanguageCode = excluded.LanguageCode,
                Repeat = excluded.Repeat,
                IsShuffleEnabled = excluded.IsShuffleEnabled,
                LastScannedFolderPath = excluded.LastScannedFolderPath;
            """;

        command.Parameters.AddWithValue("$volume", settings.VolumePercent);
        command.Parameters.AddWithValue("$muted", settings.IsMuted ? 1 : 0);
        command.Parameters.AddWithValue("$theme", settings.Theme.ToString());
        command.Parameters.AddWithValue("$language", (object?)settings.LanguageCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$repeat", settings.Repeat.ToString());
        command.Parameters.AddWithValue("$shuffle", settings.IsShuffleEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$lastFolder", (object?)settings.LastScannedFolderPath ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}