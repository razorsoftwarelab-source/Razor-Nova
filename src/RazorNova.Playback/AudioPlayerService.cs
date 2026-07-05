using System.Timers;
using NAudio.Wave;
using RazorNova.Core.Enums;
using RazorNova.Core.Interfaces;

namespace RazorNova.Playback;

/// <summary>
/// NAudio-based implementation of IAudioPlayer.
/// Uses AudioFileReader (decoding) + WaveOutEvent (output). A single
/// AudioFileReader instance transparently handles WAV, MP3, and M4A/AAC;
/// FLAC works too with zero extra packages because on Windows 10/11, Media
/// Foundation ships a native FLAC decoder that AudioFileReader falls back
/// to automatically for any extension it doesn't special-case.
/// WaveOutEvent is chosen over the older WaveOut specifically because it
/// does not require a Windows message pump on the calling thread — safe
/// to drive entirely from background threads, which matters since this
/// service is called from async ViewModel code, not necessarily the UI thread.
/// </summary>
public sealed class AudioPlayerService : IAudioPlayer
{
    private const int PositionPollIntervalMs = 200;

    private readonly object _gate = new();
    private readonly System.Timers.Timer _positionTimer;

    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioFile;

    private PlaybackStatus _status = PlaybackStatus.Stopped;
    private bool _isManualStop;
    private int _volumePercent = 100;
    private bool _isMuted;

    // Monotonic counter used to resolve a race between the position timer
    // and SeekAsync/Stop: both observe _audioFile's position while holding
    // _gate, but PositionChanged is raised AFTER the lock is released
    // (raising it while still holding _gate would deadlock — the
    // PositionChanged handler upstream reads Duration, which itself
    // re-acquires _gate, from a different thread). Releasing the lock
    // first means two observations can finish "out of order" relative to
    // which one is newer — e.g. a timer tick that read the position a
    // moment before a Seek can end up publishing its now-stale value to
    // the UI AFTER the Seek's fresh value, visually snapping the slider
    // back. _positionSequence is incremented inside _gate at the exact
    // moment a position is observed, so it reflects the lock's already-
    // correct ordering of "who saw the truth more recently." PublishPosition
    // then only forwards an observation if nothing fresher has been
    // published yet, dropping stale ones instead of letting them reach the UI.
    private long _positionSequence;
    private long _lastPublishedSequence;

    public AudioPlayerService()
    {
        _positionTimer = new System.Timers.Timer(PositionPollIntervalMs) { AutoReset = true };
        _positionTimer.Elapsed += OnPositionTimerElapsed;
    }

    public PlaybackStatus Status
    {
        get { lock (_gate) return _status; }
    }

    public TimeSpan CurrentPosition
    {
        get { lock (_gate) return _audioFile?.CurrentTime ?? TimeSpan.Zero; }
    }

    public TimeSpan Duration
    {
        get { lock (_gate) return _audioFile?.TotalTime ?? TimeSpan.Zero; }
    }

    public int VolumePercent
    {
        get => _volumePercent;
        set
        {
            _volumePercent = Math.Clamp(value, 0, 100);
            lock (_gate)
            {
                if (!_isMuted && _audioFile is not null)
                    _audioFile.Volume = _volumePercent / 100f;
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted == value) return;
            _isMuted = value;
            lock (_gate)
            {
                if (_audioFile is not null)
                    _audioFile.Volume = value ? 0f : _volumePercent / 100f;
            }
        }
    }

    public event EventHandler<PlaybackStatus>? StatusChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler? TrackEnded;

    public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio file not found.", filePath);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                TearDownCurrentLocked();

                try
                {
                    _audioFile = new AudioFileReader(filePath);
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException($"Unable to decode audio file: {filePath}", ex);
                }

                _audioFile.Volume = _isMuted ? 0f : _volumePercent / 100f;

                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Init(_audioFile);

                // Loading a new track is always the newest truth — bump the
                // sequence so a position observation still in flight from
                // the PREVIOUS track can never publish over this one.
                Interlocked.Increment(ref _positionSequence);
            }
        }, cancellationToken).ConfigureAwait(false);

        SetStatus(PlaybackStatus.Stopped);
    }

    public void Play()
    {
        lock (_gate)
        {
            if (_waveOut is null) return;
            _waveOut.Play();
        }
        SetStatus(PlaybackStatus.Playing);
        _positionTimer.Start();
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_waveOut is null) return;
            _waveOut.Pause();
        }
        _positionTimer.Stop();
        SetStatus(PlaybackStatus.Paused);
    }

    public void Stop()
    {
        long sequence;
        lock (_gate)
        {
            if (_waveOut is null) return;
            _isManualStop = true;
            _waveOut.Stop();
            if (_audioFile is not null)
                _audioFile.Position = 0;
            sequence = Interlocked.Increment(ref _positionSequence);
        }
        _positionTimer.Stop();
        SetStatus(PlaybackStatus.Stopped);
        PublishPosition(TimeSpan.Zero, sequence);
    }

    public async Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        var resultPosition = TimeSpan.Zero;
        long sequence = 0;
        var hasFile = false;

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_audioFile is null) return;
                hasFile = true;

                var clamped = position < TimeSpan.Zero ? TimeSpan.Zero
                    : position > _audioFile.TotalTime ? _audioFile.TotalTime
                    : position;
                _audioFile.CurrentTime = clamped;
                resultPosition = clamped;
                sequence = Interlocked.Increment(ref _positionSequence);
            }
        }, cancellationToken).ConfigureAwait(false);

        if (hasFile)
            PublishPosition(resultPosition, sequence);
    }

    private void OnPositionTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        TimeSpan position;
        long sequence;
        lock (_gate)
        {
            if (_audioFile is null) return;
            position = _audioFile.CurrentTime;
            sequence = Interlocked.Increment(ref _positionSequence);
        }
        PublishPosition(position, sequence);
    }

    /// <summary>
    /// Forwards a position observation to PositionChanged only if no
    /// fresher-sequenced observation has been published yet — see the
    /// _positionSequence field comment for why this exists.
    /// </summary>
    private void PublishPosition(TimeSpan position, long sequence)
    {
        while (true)
        {
            var last = Interlocked.Read(ref _lastPublishedSequence);
            if (sequence <= last)
                return; // A fresher (or equally fresh) update already won — drop this stale one.

            if (Interlocked.CompareExchange(ref _lastPublishedSequence, sequence, last) == last)
                break;
            // Another thread updated _lastPublishedSequence between our read
            // and write attempt — loop and re-check against its new value.
        }

        PositionChanged?.Invoke(this, position);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _positionTimer.Stop();

        bool wasManual;
        lock (_gate)
        {
            wasManual = _isManualStop;
            _isManualStop = false;
        }

        if (e.Exception is not null)
        {
            // Output device dropped (e.g. unplugged headset). Report Stopped
            // so the UI never gets stuck showing "Playing" forever.
            SetStatus(PlaybackStatus.Stopped);
            return;
        }

        if (wasManual)
            return; // Stop() already set state and raised PositionChanged.

        // Reached end of file on its own — this is the real "track finished" signal.
        SetStatus(PlaybackStatus.Stopped);
        TrackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void SetStatus(PlaybackStatus value)
    {
        bool changed;
        lock (_gate)
        {
            changed = _status != value;
            _status = value;
        }
        if (changed)
            StatusChanged?.Invoke(this, value);
    }

    /// <summary>Must be called while already holding _gate.</summary>
    private void TearDownCurrentLocked()
    {
        if (_waveOut is not null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }
        _audioFile?.Dispose();
        _audioFile = null;
    }

    public void Dispose()
    {
        _positionTimer.Stop();
        _positionTimer.Elapsed -= OnPositionTimerElapsed;
        _positionTimer.Dispose();

        lock (_gate)
            TearDownCurrentLocked();
    }
}