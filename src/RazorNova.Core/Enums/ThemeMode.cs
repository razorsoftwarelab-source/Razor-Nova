namespace RazorNova.Core.Enums;

/// <summary>
/// Represents the visual theme mode of the application.
/// Used by IThemeManager to decide which color palette
/// (Midnight Purple — Night or Day variant) is currently applied.
/// </summary>
public enum ThemeMode
{
    /// <summary>Matte black background (#0D0D0D) with neon orange accents (#FF8800).</summary>
    Night,

    /// <summary>Pure white background (#FFFFFF) with midnight blue accents (#1A2B4C).</summary>
    Day,

    /// <summary>
    /// Automatically follows the Windows system dark/light theme setting.
    /// IThemeManager resolves this to Night or Day at runtime and listens
    /// for OS theme-change notifications.
    /// </summary>
    System
}