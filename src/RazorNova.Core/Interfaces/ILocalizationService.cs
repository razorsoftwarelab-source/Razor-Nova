namespace RazorNova.Core.Interfaces;

/// <summary>
/// Provides UI string lookups for the app's two supported languages
/// (Persian "fa" and English "en"). Implemented by RazorNova.Localization
/// using .resx resource files. The App project's XAML binds to this via a
/// markup extension / binding proxy rather than referencing resx files
/// directly, so every other project can stay language-agnostic.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// The active language code ("fa" or "en"). On first run (no saved
    /// preference in AppSettings), this is auto-detected from the
    /// Windows OS UI culture, defaulting to "en" if neither matches.
    /// </summary>
    string CurrentLanguageCode { get; }

    /// <summary>
    /// All language codes the app has translations for, in display order.
    /// Drives the language-selection UI (e.g. a toggle or dropdown).
    /// Currently: ["fa", "en"].
    /// </summary>
    IReadOnlyList<string> AvailableLanguageCodes { get; }

    /// <summary>
    /// Raised whenever CurrentLanguageCode changes. The App project
    /// re-evaluates all bound strings (and switches FlowDirection to
    /// RightToLeft for "fa") in response to this.
    /// </summary>
    event EventHandler<string>? LanguageChanged;

    /// <summary>
    /// Looks up a localized string by key (e.g. "Play_Button_Tooltip").
    /// Returns the key itself, wrapped in brackets (e.g. "[Play_Button_Tooltip]"),
    /// if no translation is found for the current language — making
    /// missing translations obvious during testing rather than silently blank.
    /// </summary>
    string GetString(string key);

    /// <summary>
    /// Switches the active language and persists the choice (via
    /// ISettingsService). Pass "fa" or "en". Raises LanguageChanged.
    /// </summary>
    void SetLanguage(string languageCode);
}