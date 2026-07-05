namespace RazorNova.Core.Interfaces;

/// <summary>
/// Listens system-wide for hardware media keys (Play/Pause, Next,
/// Previous) on the keyboard, even while the app is minimized or not
/// focused. Implemented by RazorNova.Platform using a Win32 low-level
/// keyboard hook (WH_KEYBOARD_LL), per the chosen approach. This
/// interface exposes none of that Win32 detail — just three simple events.
/// </summary>
public interface IMediaKeyListener : IDisposable
{
    /// <summary>
    /// Installs the low-level keyboard hook and begins raising events.
    /// Call once during app startup. Safe to call only once per instance.
    /// </summary>
    void Start();

    /// <summary>Uninstalls the hook. Also called automatically by Dispose.</summary>
    void Stop();

    /// <summary>Raised when the hardware Play/Pause media key is pressed.</summary>
    event EventHandler? PlayPausePressed;

    /// <summary>Raised when the hardware Next Track media key is pressed.</summary>
    event EventHandler? NextPressed;

    /// <summary>Raised when the hardware Previous Track media key is pressed.</summary>
    event EventHandler? PreviousPressed;
}