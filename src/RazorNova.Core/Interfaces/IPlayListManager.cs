using RazorNova.Core.Enums;
using RazorNova.Core.Models;

namespace RazorNova.Core.Interfaces;

/// <summary>
/// Manages the live, in-memory playback queue: which track is current,
/// shuffle ordering, and repeat behavior. This interface knows nothing
/// about audio decoding — it only decides "which Track is next." The App
/// project wires this together with IAudioPlayer: when this manager says
/// the current track changed, the App tells IAudioPlayer to load and
/// play that track's file path.
/// </summary>
public interface IPlaylistManager
{
    /// <summary>The full queue of tracks currently loaded for playback, in original (non-shuffled) order.</summary>
    IReadOnlyList<Track> Queue { get; }

    /// <summary>The track currently selected for playback. Null if the queue is empty.</summary>
    Track? CurrentTrack { get; }

    /// <summary>Index of CurrentTrack within Queue. -1 if the queue is empty.</summary>
    int CurrentIndex { get; }

    /// <summary>
    /// Whether shuffle is enabled. Toggling this does not change
    /// CurrentTrack — it only changes which track MoveNext() picks afterward.
    /// </summary>
    bool IsShuffleEnabled { get; set; }

    /// <summary>Controls behavior when the current track finishes naturally. See HandleTrackEnded.</summary>
    RepeatMode Repeat { get; set; }

    /// <summary>
    /// Raised whenever CurrentTrack changes, for any reason (manual
    /// next/previous, natural track-end advance, or a brand-new queue
    /// being set). Carries the new current track (null if queue became empty).
    /// </summary>
    event EventHandler<Track?>? CurrentTrackChanged;

    /// <summary>
    /// Replaces the entire queue (e.g. user double-clicked a track in the
    /// library, or loaded a saved playlist) and sets CurrentTrack to the
    /// track at <paramref name="startIndex"/>. Raises CurrentTrackChanged.
    /// </summary>
    void SetQueue(IReadOnlyList<Track> tracks, int startIndex = 0);

    /// <summary>
    /// Manually advances to the next track (user pressed "Next"). If
    /// shuffle is enabled, picks the next track in shuffle order rather
    /// than sequential order. Always advances regardless of Repeat —
    /// RepeatMode.One only affects natural track-end (see HandleTrackEnded),
    /// not an explicit manual skip. Raises CurrentTrackChanged.
    /// </summary>
    /// <returns>The new CurrentTrack, or null if the queue is empty.</returns>
    Track? MoveNext();

    /// <summary>
    /// Manually moves to the previous track (user pressed "Previous").
    /// Raises CurrentTrackChanged.
    /// </summary>
    /// <returns>The new CurrentTrack, or null if the queue is empty.</returns>
    Track? MovePrevious();

    /// <summary>
    /// Called by the App when IAudioPlayer.TrackEnded fires (the track
    /// finished playing on its own, not via a manual skip). Applies
    /// Repeat logic: RepeatMode.One returns the same track again,
    /// RepeatMode.All/Off advances like MoveNext (Off returns null once
    /// the last track in the queue has finished, stopping playback).
    /// Raises CurrentTrackChanged when the track changes.
    /// </summary>
    /// <returns>The track to play next, or null if playback should stop.</returns>
    Track? HandleTrackEnded();
}