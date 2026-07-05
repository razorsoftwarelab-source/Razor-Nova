namespace RazorNova.Core.Models;

/// <summary>
/// Summary of a manual "add files" operation — the multi-file-select
/// counterpart to ScanResult. Kept as a separate type rather than reusing
/// ScanResult because there's no single folder to report here; forcing an
/// unrelated FolderPath field onto this result just to reuse ScanResult
/// would be the wrong kind of code reuse. Unlike a folder scan, a file
/// with an unsupported extension or that no longer exists on disk counts
/// toward FailedFilesCount here rather than being silently skipped — the
/// caller explicitly chose these exact files.
/// </summary>
public sealed class AddFilesResult
{
    public IReadOnlyList<Track> Tracks { get; set; } = new List<Track>();
    public int FailedFilesCount { get; set; }
}