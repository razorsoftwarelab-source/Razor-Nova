namespace RazorNova.Core.Interfaces;

/// <summary>
/// Manages the system tray icon and its right-click context menu
/// (Play/Pause, Next, Previous, Exit). Implemented by RazorNova.Platform
/// using H.NotifyIcon.Wpf. This interface exposes no WPF/tray-library
/// detail — just lifecycle methods, a way to reflect playback state in
/// the icon/tooltip, and events for menu actions.
/// </summary>
public interface ITrayService : IDisposable
{
    /// <summary>
    /// Creates and shows the tray icon with its context menu. Call once
    /// during app startup (the icon stays visible even when minimized to tray).
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the tray icon's tooltip text — typically "Artist – Title"
    /// of the current track, or the app name when nothing is playing.
    /// </summary>
    void UpdateTooltip(string text);

    /// <summary>
    /// Updates the Play/Pause entry in the context menu to reflect
    /// whether the app is currently playing, so the menu label/icon
    /// (Play vs Pause) always matches actual playback state.
    /// </summary>
    void UpdatePlayPauseState(bool isPlaying);

    /// <summary>Shows a brief Windows notification balloon/toast (e.g. on track change), if desired.</summary>
    void ShowNotification(string title, string message);

    /// <summary>Removes the tray icon. Also called automatically by Dispose.</summary>
    void Remove();

    /// <summary>Raised when the user clicks "Play/Pause" in the tray context menu.</summary>
    event EventHandler? PlayPauseRequested;

    /// <summary>Raised when the user clicks "Next" in the tray context menu.</summary>
    event EventHandler? NextRequested;

    /// <summary>Raised when the user clicks "Previous" in the tray context menu.</summary>
    event EventHandler? PreviousRequested;

    /// <summary>Raised when the user clicks "Exit" in the tray context menu — the app should shut down gracefully.</summary>
    event EventHandler? ExitRequested;

    /// <summary>
    /// Raised when the user double-clicks (or single-clicks, per Windows
    /// convention) the tray icon itself — typically used to restore the
    /// main window from a minimized/tray state.
    /// </summary>
    event EventHandler? TrayIconActivated;
}