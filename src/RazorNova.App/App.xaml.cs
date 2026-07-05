using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RazorNova.App.ViewModels;
using RazorNova.App.Views;
using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;
using RazorNova.Data;
using RazorNova.Library;
using RazorNova.Metadata;
using RazorNova.Platform;
using RazorNova.Playback;
using RazorNova.Playlist;
using RazorNova.Theme;
using System.Windows.Controls;

namespace RazorNova.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private MainWindow? _mainWindow;
    private MiniPlayerWindow? _miniPlayerWindow;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var settings = await settingsService.LoadAsync();

        var themeManager = _serviceProvider.GetRequiredService<IThemeManager>();
        themeManager.SelectedTheme = settings.Theme;
        themeManager.ThemeResolved += (_, resolvedTheme) => ApplyThemeDictionary(resolvedTheme);
        themeManager.StartListeningForSystemThemeChanges();
        ApplyThemeDictionary(themeManager.ResolvedTheme);

        var playerControls = _serviceProvider.GetRequiredService<PlayerControlsViewModel>();
        playerControls.VolumePercent = settings.VolumePercent;
        playerControls.IsMuted = settings.IsMuted;
        playerControls.IsShuffleEnabled = settings.IsShuffleEnabled;
        playerControls.Repeat = settings.Repeat;

        _serviceProvider.GetRequiredService<IMediaKeyListener>().Start();

        // Tray service events subscribed immediately,
        // but initialization is delayed until after MainWindow is shown.
        var trayService = _serviceProvider.GetRequiredService<ITrayService>();
        trayService.ExitRequested += (_, _) => Shutdown();
        trayService.TrayIconActivated += (_, _) => RestoreMainWindow();

        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        await mainViewModel.InitializeAsync();

        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        _mainWindow.DataContext = mainViewModel;
        _mainWindow.StateChanged += OnMainWindowStateChanged;
        _mainWindow.Closed += (_, _) => Shutdown();
        _mainWindow.Show();

        // Now that a Window has been shown, WPF's render thread is alive,
        // so RenderTargetBitmap.Render() inside Initialize() will succeed.
        trayService.Initialize();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddConsole());

        services.AddSingleton(new DatabaseContext());
        services.AddSingleton<ITrackRepository, TrackRepository>();
        services.AddSingleton<IPlaylistRepository, PlaylistRepository>();
        services.AddSingleton<ISettingsService, SettingsRepository>();

        services.AddSingleton<IAudioPlayer, AudioPlayerService>();
        services.AddSingleton<IMetadataReader, MetadataReaderService>();
        services.AddSingleton<IAlbumArtService, AlbumArtCacheService>();
        services.AddSingleton<ILibraryService, FolderScannerService>();
        services.AddSingleton<IPlaylistManager, PlaylistManagerService>();
        services.AddSingleton<IThemeManager, ThemeManagerService>();
        // ILocalizationService removed
        services.AddSingleton<IMediaKeyListener, MediaKeyListener>();
        services.AddSingleton<ITrayService, TrayIconService>();
        services.AddSingleton<PlayerControlsViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<PlaylistViewModel>();
        services.AddSingleton<MainViewModel>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MiniPlayerWindow>();
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow?.WindowState == WindowState.Minimized)
            ShowMiniPlayer();
    }

    private void ShowMiniPlayer()
    {
        _miniPlayerWindow ??= CreateMiniPlayerWindow();

        var workArea = SystemParameters.WorkArea;
        _miniPlayerWindow.Left = workArea.Right - _miniPlayerWindow.Width - 16;
        _miniPlayerWindow.Top = workArea.Bottom - _miniPlayerWindow.Height - 16;

        _mainWindow?.Hide();
        _miniPlayerWindow.Show();
    }

    private MiniPlayerWindow CreateMiniPlayerWindow()
    {
        var window = _serviceProvider?.GetRequiredService<MiniPlayerWindow>()
            ?? throw new InvalidOperationException("ServiceProvider is null");
        window.DataContext = _serviceProvider.GetRequiredService<PlayerControlsViewModel>();
        window.RestoreRequested += (_, _) => RestoreMainWindow();
        return window;
    }

    private void RestoreMainWindow()
    {
        _miniPlayerWindow?.Hide();

        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled exception on the UI thread.");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "Razor Nova", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void ApplyThemeDictionary(ThemeMode resolvedTheme)
    {
        var themeUri = resolvedTheme == ThemeMode.Night
            ? new Uri("Themes/NightTheme.xaml", UriKind.Relative)
            : new Uri("Themes/DayTheme.xaml", UriKind.Relative);

        var newDictionary = new ResourceDictionary { Source = themeUri };

        var merged = Resources.MergedDictionaries;
        var existingTheme = merged.FirstOrDefault(d => d.Contains("WindowBackgroundColor"));
        if (existingTheme is not null)
            merged.Remove(existingTheme);

        merged.Add(newDictionary);

        if (merged.All(d => !d.Contains("ThemedSliderStyle")))
        {
            merged.Add(new ResourceDictionary
            {
                Source = new Uri("Styles/ControlStyles.xaml", UriKind.Relative)
            });
        }

        if (!Resources.Contains("ByteArrayToImageSourceConverter"))
            Resources["ByteArrayToImageSourceConverter"] = new RazorNova.App.Converters.ByteArrayToImageSourceConverter();
        if (!Resources.Contains("EnumToBooleanConverter"))
            Resources["EnumToBooleanConverter"] = new RazorNova.App.Converters.EnumToBooleanConverter();
        if (!Resources.Contains("TimeSpanToShortStringConverter"))
            Resources["TimeSpanToShortStringConverter"] = new RazorNova.App.Converters.TimeSpanToShortStringConverter();
        if (!Resources.Contains("InverseBooleanConverter"))
            Resources["InverseBooleanConverter"] = new RazorNova.App.Converters.InverseBooleanConverter();
        if (!Resources.Contains("BooleanToVisibilityConverter"))
            Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}