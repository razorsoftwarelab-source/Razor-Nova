using Microsoft.Data.Sqlite;

namespace RazorNova.Data;

/// <summary>
/// Owns the SQLite connection string and one-time schema creation for the
/// RazorNova library database. Every repository in this project (Track,
/// Playlist, Settings) goes through this class to open connections — none
/// of them talk to SQLite directly on their own. Registered as a singleton
/// in the App project's DI container and injected into each repository's constructor.
/// </summary>
public sealed class DatabaseContext
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;

    public DatabaseContext(string? databaseFilePath = null)
    {
        var path = databaseFilePath ?? GetDefaultDatabasePath();

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    /// <summary>%LocalAppData%\RazorNova\library.db — used when no explicit path is supplied.</summary>
    public static string GetDefaultDatabasePath()
    {
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataFolder, "RazorNova", "library.db");
    }

    /// <summary>
    /// Opens a new, ready-to-use connection. Ensures the schema exists
    /// first (only does real work on the very first call). Callers are
    /// expected to open a short-lived connection per operation and dispose
    /// it promptly — SQLite connections are cheap, and this avoids holding
    /// a single shared connection across concurrent async operations.
    /// </summary>
    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isInitialized) return;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using (var pragmaCommand = connection.CreateCommand())
            {
                // WAL lets readers (e.g. the library list) keep working
                // smoothly while a write (e.g. a folder scan inserting
                // hundreds of tracks) is in progress, instead of blocking.
                pragmaCommand.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
                await pragmaCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await CreateSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string schemaSql = """
            CREATE TABLE IF NOT EXISTS Tracks (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                FilePath          TEXT    NOT NULL UNIQUE,
                Title             TEXT    NOT NULL,
                Artist            TEXT    NOT NULL DEFAULT '',
                Album             TEXT    NOT NULL DEFAULT '',
                DurationTicks     INTEGER NOT NULL DEFAULT 0,
                FileModifiedTicks INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS IX_Tracks_Title  ON Tracks (Title);
            CREATE INDEX IF NOT EXISTS IX_Tracks_Artist ON Tracks (Artist);
            CREATE INDEX IF NOT EXISTS IX_Tracks_Album  ON Tracks (Album);

            CREATE TABLE IF NOT EXISTS Playlists (
                Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                Name           TEXT    NOT NULL,
                TrackIdsCsv    TEXT    NOT NULL DEFAULT '',
                CreatedAtTicks INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AppSettings (
                Id                    INTEGER PRIMARY KEY CHECK (Id = 1),
                VolumePercent         INTEGER NOT NULL DEFAULT 80,
                IsMuted               INTEGER NOT NULL DEFAULT 0,
                Theme                 TEXT    NOT NULL DEFAULT 'System',
                LanguageCode          TEXT    NULL,
                Repeat                TEXT    NOT NULL DEFAULT 'Off',
                IsShuffleEnabled      INTEGER NOT NULL DEFAULT 0,
                LastScannedFolderPath TEXT    NULL
            );
            """;

        using var command = connection.CreateCommand();
        command.CommandText = schemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}