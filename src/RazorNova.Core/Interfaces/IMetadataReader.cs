using RazorNova.Core.Models;

namespace RazorNova.Core.Interfaces;

/// <summary>
/// Reads textual metadata (title, artist, album, duration) from an audio
/// file's tags. Implemented by RazorNova.Metadata using TagLib#. Used by
/// RazorNova.Library during folder scanning to populate a Track for each
/// file found. Deliberately does NOT handle album art — that is
/// IAlbumArtService's responsibility, even though both are implemented
/// using TagLib# under the hood in the same project.
/// </summary>
public interface IMetadataReader
{
    /// <summary>
    /// Reads tags from the file at <paramref name="filePath"/> and
    /// returns a populated Track (Id is left as 0 — the caller persists
    /// it via ITrackRepository to obtain a real Id). If a tag field is
    /// missing or empty (e.g. no Title tag), Title falls back to the
    /// file name without its extension, so the track is never displayed
    /// blank in the library list.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">
    /// The file is corrupt, locked, or its tag data could not be parsed.
    /// Callers (e.g. the folder scanner) should catch this per-file and
    /// count it toward ScanResult.FailedFilesCount rather than aborting the whole scan.
    /// </exception>
    Task<Track> ReadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether <paramref name="filePath"/> has one of the
    /// supported extensions (.mp3, .flac, .wav, .m4a) based on its
    /// extension alone — no file I/O. Used by the folder scanner to
    /// cheaply filter out irrelevant files before attempting ReadAsync
    /// on each one.
    /// </summary>
    bool IsSupportedExtension(string filePath);
}