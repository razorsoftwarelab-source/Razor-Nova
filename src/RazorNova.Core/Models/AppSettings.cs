using RazorNova.Core.Enums;

namespace RazorNova.Core.Models;

/// <summary>
/// Persisted, user-level application settings. Loaded once at startup
/// by ISettingsService and saved whenever the user changes a relevant
/// option. This is the single source of truth for "remembered state"
/// between app launches (volume, theme, language, last playback mode).
/// </summary>
public sealed class AppSettings
{
    /// <summary>Output volume as a percentage, 0–100.</summary>
    public int VolumePercent { get; set; } = 80;

    /// <summary>Whether output is currently muted (volume is preserved, not zeroed, while muted).</summary>
    public bool IsMuted { get; set; } = false;

    /// <summary>Selected theme mode: Night, Day, or System (auto-follow Windows).</summary>
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>
    /// UI language code: "fa" or "en". Null means "not yet chosen by the
    /// user" — ILocalizationService will auto-detect from the OS in that case.
    /// </summary>
    public string? LanguageCode { get; set; } = null;

    /// <summary>Last repeat mode the user had selected, restored on next launch.</summary>
    public RepeatMode Repeat { get; set; } = RepeatMode.Off;

    /// <summary>Whether shuffle was enabled, restored on next launch.</summary>
    public bool IsShuffleEnabled { get; set; } = false;

    /// <summary>
    /// The last folder the user scanned via the folder dialog, offered
    /// as a convenient default the next time they open that dialog.
    /// Null if the user has never scanned a folder.
    /// </summary>
    public string? LastScannedFolderPath { get; set; } = null;
}