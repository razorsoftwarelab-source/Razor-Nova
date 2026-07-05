namespace RazorNova.Core.Enums;

/// <summary>
/// Represents the current state of the audio playback engine.
/// Used by IAudioPlayer to report status and by the UI to update
/// Play/Pause icons reactively.
/// </summary>
public enum PlaybackStatus
{
    /// <summary>No track is loaded, or playback has been fully stopped and reset to position zero.</summary>
    Stopped,

    /// <summary>A track is actively playing audio.</summary>
    Playing,

    /// <summary>A track is loaded and position is held, but audio output is paused.</summary>
    Paused
}