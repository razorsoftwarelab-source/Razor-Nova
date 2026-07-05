namespace RazorNova.Core.Enums;

/// <summary>
/// Defines the available sorting criteria for tracks shown in the
/// Library view and within a Playlist. Used by ILibraryService and
/// IPlaylistManager so both can share a single, consistent sort contract.
/// </summary>
public enum SortCriteria
{
    /// <summary>Sort alphabetically by track title (A → Z).</summary>
    Name,

    /// <summary>Sort by the audio file's last-modified date (newest first).</summary>
    ModifiedDate
}