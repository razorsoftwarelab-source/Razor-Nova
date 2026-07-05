using RazorNova.Core.Models;

namespace RazorNova.Core.Interfaces;

/// <summary>
/// Loads and persists the single AppSettings record (volume, theme,
/// language, repeat/shuffle state, last-scanned folder). Implemented by
/// RazorNova.Data alongside ITrackRepository/IPlaylistRepository, since
/// settings are stored in the same SQLite database. Deliberately a
/// distinct interface from the other two repositories: it manages exactly
/// one record with no Id/CRUD semantics, not a collection.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads the persisted settings. If no settings have ever been saved
    /// (first run), returns a new AppSettings with its documented
    /// defaults rather than throwing or returning null.
    /// </summary>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the given settings, overwriting any previously saved
    /// values. Callers (IThemeManager, ILocalizationService,
    /// IPlaylistManager, the volume control, etc.) call this whenever a
    /// setting they own changes, so it survives an app restart.
    /// </summary>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}