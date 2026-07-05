using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;

namespace RazorNova.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IThemeManager _themeManager;
    private readonly IMediaKeyListener _mediaKeyListener;
    private readonly ITrayService _trayService;

    public PlayerControlsViewModel PlayerControls { get; }
    public LibraryViewModel Library { get; }
    public PlaylistViewModel Playlist { get; }

    public MainViewModel(
        PlayerControlsViewModel playerControls,
        LibraryViewModel library,
        PlaylistViewModel playlist,
        IThemeManager themeManager,
        IMediaKeyListener mediaKeyListener,
        ITrayService trayService)
    {
        PlayerControls = playerControls;
        Library = library;
        Playlist = playlist;
        _themeManager = themeManager;
        _mediaKeyListener = mediaKeyListener;
        _trayService = trayService;

        _mediaKeyListener.PlayPausePressed += (_, _) => PlayerControls.PlayPauseCommand.Execute(null);
        _mediaKeyListener.NextPressed += (_, _) => PlayerControls.NextCommand.Execute(null);
        _mediaKeyListener.PreviousPressed += (_, _) => PlayerControls.PreviousCommand.Execute(null);

        _trayService.PlayPauseRequested += (_, _) => PlayerControls.PlayPauseCommand.Execute(null);
        _trayService.NextRequested += (_, _) => PlayerControls.NextCommand.Execute(null);
        _trayService.PreviousRequested += (_, _) => PlayerControls.PreviousCommand.Execute(null);

        PlayerControls.PropertyChanged += OnPlayerControlsPropertyChanged;
        UpdateTrayState();
    }

    public ThemeMode SelectedTheme
    {
        get => _themeManager.SelectedTheme;
        set
        {
            if (_themeManager.SelectedTheme == value) return;
            _themeManager.SelectedTheme = value;
            OnPropertyChanged();
        }
    }

    public Task InitializeAsync() => Task.WhenAll(Library.InitializeAsync(), Playlist.InitializeAsync());

    private void OnPlayerControlsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerControlsViewModel.Status)
            or nameof(PlayerControlsViewModel.TrackTitle)
            or nameof(PlayerControlsViewModel.TrackArtist))
        {
            UpdateTrayState();
        }
    }

    private void UpdateTrayState()
    {
        _trayService.UpdatePlayPauseState(PlayerControls.IsPlaying);

        var tooltip = string.IsNullOrEmpty(PlayerControls.TrackArtist)
            ? PlayerControls.TrackTitle
            : $"{PlayerControls.TrackArtist} \u2013 {PlayerControls.TrackTitle}";
        _trayService.UpdateTooltip(tooltip);
    }

    public void Dispose()
    {
        PlayerControls.PropertyChanged -= OnPlayerControlsPropertyChanged;
        PlayerControls.Dispose();
    }
}