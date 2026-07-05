using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;

namespace RazorNova.App.ViewModels;

/// <summary>
/// Drives playback controls: play/pause/stop/next/previous, seeking,
/// volume/mute, shuffle, repeat, the elapsed/remaining time toggle, and
/// the current track's title/artist/album art. Bridges IAudioPlayer and
/// IPlaylistManager — which never reference each other — by reacting to
/// IPlaylistManager.CurrentTrackChanged (load + play whatever track is
/// now current) and IAudioPlayer.TrackEnded (ask IPlaylistManager what
/// should play next).
///
/// Registered as a singleton and shared between MainWindow and
/// MiniPlayerWindow (see the App composition root) so both windows always
/// reflect identical playback state with no extra sync code.
/// </summary>
public sealed partial class PlayerControlsViewModel : ObservableObject, IDisposable
{
    private readonly IAudioPlayer _audioPlayer;
    private readonly IPlaylistManager _playlistManager;
    private readonly IAlbumArtService _albumArtService;
    private readonly ISettingsService _settingsService;
    private readonly Dispatcher _dispatcher;

    // Set right before MoveNext()/MovePrevious() when the player was
    // Stopped at the moment of the click. HandleTrackChangedAsync reads
    // and clears it once, deciding whether the newly-current track should
    // auto-play. This is the entire fix for "Next/Previous after Stop
    // starts playback" — every other trigger of CurrentTrackChanged
    // (natural track-end advancing, SetQueue, etc.) is unaffected and
    // keeps auto-playing exactly as before.
    private bool _suppressAutoPlayOnTrackChange;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying))]
    private PlaybackStatus _status = PlaybackStatus.Stopped;

    [ObservableProperty]
    private string _trackTitle = "—";

    [ObservableProperty]
    private string _trackArtist = string.Empty;

    [ObservableProperty]
    private byte[]? _albumArtData;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeDisplayText))]
    private TimeSpan _currentPosition;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeDisplayText))]
    [NotifyPropertyChangedFor(nameof(TotalDurationText))]
    private TimeSpan _duration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeDisplayText))]
    private bool _isShowingRemainingTime;

    [ObservableProperty]
    private int _volumePercent = 80;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isShuffleEnabled;

    [ObservableProperty]
    private RepeatMode _repeat = RepeatMode.Off;

    public bool IsPlaying => Status == PlaybackStatus.Playing;

    public string TimeDisplayText => IsShowingRemainingTime
        ? $"-{FormatTime(Duration - CurrentPosition)}"
        : FormatTime(CurrentPosition);

    /// <summary>Always shows the track's full length, independent of the
    /// elapsed/remaining toggle above — e.g. for a "1:23 / 3:45" style
    /// readout in the XAML next to the seek slider.</summary>
    public string TotalDurationText => FormatTime(Duration);

    public PlayerControlsViewModel(
        IAudioPlayer audioPlayer,
        IPlaylistManager playlistManager,
        IAlbumArtService albumArtService,
        ISettingsService settingsService)
    {
        _audioPlayer = audioPlayer;
        _playlistManager = playlistManager;
        _albumArtService = albumArtService;
        _settingsService = settingsService;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _audioPlayer.StatusChanged += OnAudioStatusChanged;
        _audioPlayer.PositionChanged += OnAudioPositionChanged;
        _audioPlayer.TrackEnded += OnAudioTrackEnded;
        _playlistManager.CurrentTrackChanged += OnCurrentTrackChanged;
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (_playlistManager.CurrentTrack is null) return;

        if (Status == PlaybackStatus.Playing)
            _audioPlayer.Pause();
        else
            _audioPlayer.Play();
    }

    [RelayCommand]
    private void Stop() => _audioPlayer.Stop();

    [RelayCommand]
    private void Next()
    {
        _suppressAutoPlayOnTrackChange = Status == PlaybackStatus.Stopped;
        _playlistManager.MoveNext();
    }

    [RelayCommand]
    private void Previous()
    {
        _suppressAutoPlayOnTrackChange = Status == PlaybackStatus.Stopped;
        _playlistManager.MovePrevious();
    }

    [RelayCommand]
    private Task SeekAsync(double newPositionSeconds) =>
        _audioPlayer.SeekAsync(TimeSpan.FromSeconds(newPositionSeconds));

    [RelayCommand]
    private void ToggleTimeDisplay() => IsShowingRemainingTime = !IsShowingRemainingTime;

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void SetVolume(int newVolumePercent) => VolumePercent = Math.Clamp(newVolumePercent, 0, 100);

    [RelayCommand]
    private void ToggleShuffle() => IsShuffleEnabled = !IsShuffleEnabled;

    [RelayCommand]
    private void CycleRepeat() => Repeat = Repeat switch
    {
        RepeatMode.Off => RepeatMode.All,
        RepeatMode.All => RepeatMode.One,
        RepeatMode.One => RepeatMode.Off,
        _ => RepeatMode.Off
    };

    partial void OnIsMutedChanged(bool value)
    {
        _audioPlayer.IsMuted = value;
        _ = PersistAsync(s => s.IsMuted = value);
    }

    partial void OnVolumePercentChanged(int value)
    {
        _audioPlayer.VolumePercent = value;
        _ = PersistAsync(s => s.VolumePercent = value);
    }

    partial void OnIsShuffleEnabledChanged(bool value)
    {
        _playlistManager.IsShuffleEnabled = value;
        _ = PersistAsync(s => s.IsShuffleEnabled = value);
    }

    partial void OnRepeatChanged(RepeatMode value)
    {
        _playlistManager.Repeat = value;
        _ = PersistAsync(s => s.Repeat = value);
        Debug.WriteLine($"[RazorNova] Repeat changed to: {value}");
    }

    private void OnCurrentTrackChanged(object? sender, Track? track) =>
        _dispatcher.Invoke(() => _ = HandleTrackChangedAsync(track));

    private async Task HandleTrackChangedAsync(Track? track)
    {
        // Read and clear immediately — this must be captured before any
        // await, since it reflects the click that caused THIS specific
        // track change, not whatever happens to be pending later.
        var autoPlay = !_suppressAutoPlayOnTrackChange;
        _suppressAutoPlayOnTrackChange = false;

        if (track is null)
        {
            TrackTitle = "—";
            TrackArtist = string.Empty;
            AlbumArtData = _albumArtService.GetDefaultCover().ImageData;
            _audioPlayer.Stop();
            return;
        }

        TrackTitle = track.Title;
        TrackArtist = track.Artist;

        var art = await _albumArtService.GetAlbumArtAsync(track);
        AlbumArtData = art.ImageData;

        await LoadTrackAsync(track, autoPlay);
    }

    private void OnAudioStatusChanged(object? sender, PlaybackStatus status) =>
        _dispatcher.Invoke(() => Status = status);

    private void OnAudioPositionChanged(object? sender, TimeSpan position) =>
        _dispatcher.Invoke(() =>
        {
            CurrentPosition = position;
            Duration = _audioPlayer.Duration;
        });

    private void OnAudioTrackEnded(object? sender, EventArgs e) =>
        _dispatcher.Invoke(() =>
        {
            Debug.WriteLine($"[RazorNova] TrackEnded fired. Repeat={Repeat}");

            var nextTrack = _playlistManager.HandleTrackEnded();

            Debug.WriteLine(
                $"[RazorNova] HandleTrackEnded() returned: {(nextTrack is null ? "null (stopping)" : nextTrack.Title)}");

            if (Repeat == RepeatMode.One && nextTrack is not null)
                _ = LoadAndPlayAsync(nextTrack);
        });

    /// <summary>Always auto-plays — used by the natural "track ended,
    /// repeat one" path where restarting playback is always the intent.</summary>
    private Task LoadAndPlayAsync(Track track) => LoadTrackAsync(track, autoPlay: true);

    private async Task LoadTrackAsync(Track track, bool autoPlay)
    {
        await _audioPlayer.LoadAsync(track.FilePath);

        // Set immediately rather than waiting for the first position-timer
        // tick — keeps the seek slider's Maximum correct from the very
        // first frame instead of sitting at 0 momentarily after load.
        Duration = _audioPlayer.Duration;

        if (autoPlay)
            _audioPlayer.Play();
    }

    private async Task PersistAsync(Action<AppSettings> applyChange)
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            applyChange(settings);
            await _settingsService.SaveAsync(settings);
        }
        catch
        {
            // Best-effort persistence — a failed save must never crash the
            // app or block the control from updating visually.
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        return time.Hours > 0 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }

    public void Dispose()
    {
        _audioPlayer.StatusChanged -= OnAudioStatusChanged;
        _audioPlayer.PositionChanged -= OnAudioPositionChanged;
        _audioPlayer.TrackEnded -= OnAudioTrackEnded;
        _playlistManager.CurrentTrackChanged -= OnCurrentTrackChanged;
    }
}