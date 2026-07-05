using Microsoft.Win32;
using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;

namespace RazorNova.Theme;

/// <summary>
/// Implements IThemeManager. When SelectedTheme is System, ResolvedTheme
/// is read live from the Windows registry (HKCU ...\Personalize\
/// AppsUseLightTheme). Listens for OS theme changes via
/// Microsoft.Win32.SystemEvents so the app reacts immediately if the user
/// flips Windows' own dark/light setting while RazorNova is running.
/// Persists SelectedTheme via ISettingsService on every change, following
/// the "load whole settings, change one field, save whole settings back"
/// pattern documented on ISettingsService.
/// </summary>
public sealed class ThemeManagerService : IThemeManager, IDisposable
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string LightThemeValueName = "AppsUseLightTheme";

    private readonly ISettingsService _settingsService;

    private ThemeMode _selectedTheme = ThemeMode.System;
    private ThemeMode _lastResolvedTheme;
    private bool _isListening;

    public ThemeManagerService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _lastResolvedTheme = ResolveTheme(_selectedTheme);
    }

    public ThemeMode SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value) return;
            _selectedTheme = value;
            PersistFireAndForget(value);
            RecomputeAndNotify();
        }
    }

    public ThemeMode ResolvedTheme => ResolveTheme(_selectedTheme);

    public event EventHandler<ThemeMode>? ThemeResolved;

    public void StartListeningForSystemThemeChanges()
    {
        if (_isListening) return;
        _isListening = true;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Theme/personalization changes surface under the General
        // category. Always re-check (not just when SelectedTheme is
        // System) so the cached _lastResolvedTheme stays accurate for
        // whenever the user switches back to System later.
        if (e.Category != UserPreferenceCategory.General) return;
        RecomputeAndNotify();
    }

    private void RecomputeAndNotify()
    {
        var resolved = ResolveTheme(_selectedTheme);
        if (resolved == _lastResolvedTheme) return;

        _lastResolvedTheme = resolved;
        ThemeResolved?.Invoke(this, resolved);
    }

    private static ThemeMode ResolveTheme(ThemeMode selected) =>
        selected != ThemeMode.System ? selected : ReadWindowsSystemTheme();

    private static ThemeMode ReadWindowsSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            if (key?.GetValue(LightThemeValueName) is int lightThemeFlag)
                return lightThemeFlag == 0 ? ThemeMode.Night : ThemeMode.Day;
        }
        catch
        {
            // Registry key unavailable or unreadable — fall through to the safe default below.
        }

        // Older Windows versions or a missing key: default to Day,
        // matching the historical out-of-the-box Windows default.
        return ThemeMode.Day;
    }

    private void PersistFireAndForget(ThemeMode theme) => _ = PersistAsync(theme);

    private async Task PersistAsync(ThemeMode theme)
    {
        try
        {
            var settings = await _settingsService.LoadAsync().ConfigureAwait(false);
            settings.Theme = theme;
            await _settingsService.SaveAsync(settings).ConfigureAwait(false);
        }
        catch
        {
            // Persisting the choice is best-effort — a failed save must
            // never crash the app or block the theme from applying visually.
        }
    }

    public void Dispose()
    {
        if (!_isListening) return;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _isListening = false;
    }
}