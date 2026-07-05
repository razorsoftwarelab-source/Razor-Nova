using RazorNova.Core.Enums;

namespace RazorNova.Core.Interfaces;

/// <summary>
/// Manages the application's visual theme (Midnight Purple — Night/Day
/// variants). Implemented by RazorNova.Theme. The App project's resource
/// dictionaries (NightTheme.xaml / DayTheme.xaml) are swapped in response
/// to this service's resolved theme, so no other project needs to know
/// about colors or XAML at all.
/// </summary>
public interface IThemeManager
{
    /// <summary>
    /// The user's selected theme preference (Night, Day, or System).
    /// Setting this persists the choice (via ISettingsService) and
    /// raises ThemeResolved with the newly resolved value.
    /// </summary>
    ThemeMode SelectedTheme { get; set; }

    /// <summary>
    /// The actual theme currently being displayed — always Night or Day,
    /// never System. When SelectedTheme is System, this reflects whatever
    /// the Windows OS dark/light setting currently resolves to.
    /// </summary>
    ThemeMode ResolvedTheme { get; }

    /// <summary>
    /// Raised whenever ResolvedTheme changes — either because the user
    /// picked a different SelectedTheme, or because SelectedTheme is
    /// System and the underlying Windows OS theme setting changed while
    /// the app is running. Always carries Night or Day (never System).
    /// </summary>
    event EventHandler<ThemeMode>? ThemeResolved;

    /// <summary>
    /// Begins listening for Windows OS theme-change notifications.
    /// Call once during app startup. Has no effect if SelectedTheme is
    /// not System, but listening starts regardless so that switching
    /// to System later works immediately without re-subscribing.
    /// </summary>
    void StartListeningForSystemThemeChanges();
}