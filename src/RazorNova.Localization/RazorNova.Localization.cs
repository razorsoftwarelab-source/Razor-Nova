using System.Globalization;
using System.Resources;
using RazorNova.Core.Interfaces;

namespace RazorNova.Localization;

/// <summary>
/// Implements ILocalizationService using a standard .NET ResourceManager
/// over Strings.resx (English, neutral) / Strings.fa.resx (Persian
/// satellite). Persists the chosen language via ISettingsService,
/// following the same "load whole settings, change one field, save back"
/// pattern used by ThemeManagerService — for the same race-condition reason.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    // Base name = {RootNamespace}.{folder}.{resx file name, no extension}.
    // The SDK embeds Strings.resx/Strings.fa.resx automatically (no extra
    // csproj wiring needed) because .resx files are implicitly included
    // as EmbeddedResource items by the default SDK item globs.
    private static readonly ResourceManager ResourceManager =
        new("RazorNova.Localization.Resources.Strings", typeof(LocalizationService).Assembly);

    private readonly ISettingsService _settingsService;
    private string _currentLanguageCode;

    public LocalizationService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _currentLanguageCode = DetectInitialLanguage();
    }

    public string CurrentLanguageCode => _currentLanguageCode;

    public IReadOnlyList<string> AvailableLanguageCodes { get; } = new[] { "fa", "en" };

    public event EventHandler<string>? LanguageChanged;

    public string GetString(string key)
    {
        var culture = CultureInfo.GetCultureInfo(_currentLanguageCode);
        var value = ResourceManager.GetString(key, culture);
        return value ?? $"[{key}]";
    }

    public void SetLanguage(string languageCode)
    {
        if (!AvailableLanguageCodes.Contains(languageCode, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported language code: '{languageCode}'.", nameof(languageCode));

        if (string.Equals(_currentLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            return;

        _currentLanguageCode = languageCode;
        PersistFireAndForget(languageCode);
        LanguageChanged?.Invoke(this, languageCode);
    }

    private static string DetectInitialLanguage()
    {
        var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return systemLanguage.Equals("fa", StringComparison.OrdinalIgnoreCase) ? "fa" : "en";
    }

    private void PersistFireAndForget(string languageCode) => _ = PersistAsync(languageCode);

    private async Task PersistAsync(string languageCode)
    {
        try
        {
            var settings = await _settingsService.LoadAsync().ConfigureAwait(false);
            settings.LanguageCode = languageCode;
            await _settingsService.SaveAsync(settings).ConfigureAwait(false);
        }
        catch
        {
            // Persisting the choice is best-effort — a failed save must
            // never crash the app or block the language from switching visually.
        }
    }
}