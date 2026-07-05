using RazorNova.Core.Enums;

namespace RazorNova.Core.Interfaces;

/// <summary>
/// Abstracts the audio playback engine. Implemented by RazorNova.Playback
/// using NAudio. Operates on a single "currently loaded" file at a time —
/// it has no concept of playlists, shuffle, or repeat; that orchestration
/// belongs to IPlaylistManager, which decides *which* file to load next
/// and calls into this interface to actually play it.
/// </summary>
public interface IAudioPlayer : IDisposable
{
    /// <summary>Current playback state (Stopped / Playing / Paused).</summary>
    PlaybackStatus Status { get; }

    /// <summary>Current playback position within the loaded track.</summary>
    TimeSpan CurrentPosition { get; }

    /// <summary>Total length of the currently loaded track. TimeSpan.Zero if nothing is loaded.</summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Output volume as a percentage (0–100). Setting this takes effect
    /// immediately, even mid-playback.
    /// </summary>
    int VolumePercent { get; set; }

    /// <summary>
    /// Mutes/unmutes output without altering VolumePercent — unmuting
    /// restores exactly the volume that was set before muting.
    /// </summary>
    bool IsMuted { get; set; }

    /// <summary>
    /// Raised whenever Status changes (e.g. Play tapped, Pause tapped,
    /// track finishes). May be raised from a background thread —
    /// subscribers that touch the UI must marshal to the UI thread themselves.
    /// </summary>
    event EventHandler<PlaybackStatus>? StatusChanged;

    /// <summary>
    /// Raised periodically (e.g. every ~200ms) while playing, carrying the
    /// current position. Drives the UI's progress bar and elapsed/remaining
    /// time display. May be raised from a background thread.
    /// </summary>
    event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>
    /// Raised when the loaded track finishes playing naturally (not from
    /// a manual Stop). IPlaylistManager listens to this to decide whether
    /// to advance to the next track based on the current RepeatMode.
    /// </summary>
    event EventHandler? TrackEnded;

    /// <summary>
    /// Loads an audio file (MP3, FLAC, WAV, or M4A) and prepares it for
    /// playback, but does not start playing. Replaces any previously
    /// loaded track. Runs decoder/header initialization off the UI thread.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="NotSupportedException">The file format/codec is not supported.</exception>
    Task LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Starts or resumes playback of the currently loaded track.</summary>
    void Play();

    /// <summary>Pauses playback, preserving CurrentPosition.</summary>
    void Pause();

    /// <summary>Stops playback and resets CurrentPosition to zero.</summary>
    void Stop();

    /// <summary>
    /// Seeks to the given position within the loaded track without
    /// blocking the UI thread. Safe to call while playing or paused.
    /// </summary>
    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
}