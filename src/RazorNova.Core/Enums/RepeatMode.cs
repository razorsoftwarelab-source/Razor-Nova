namespace RazorNova.Core.Enums;

/// <summary>
/// Controls how the playlist behaves when a track finishes playing.
/// Used by IPlaylistManager to decide which track to advance to next.
/// </summary>
public enum RepeatMode
{
    /// <summary>Playback stops after the current playlist finishes (no repetition).</summary>
    Off,

    /// <summary>The current track repeats indefinitely when it finishes.</summary>
    One,

    /// <summary>The entire playlist repeats from the beginning after the last track finishes.</summary>
    All
}