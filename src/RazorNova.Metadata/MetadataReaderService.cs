using RazorNova.Core.Interfaces;
using RazorNova.Core.Models;
using TagLibFile = TagLib.File;

namespace RazorNova.Metadata;

/// <summary>
/// TagLib#-based implementation of IMetadataReader. Reads Title, Artist,
/// Album, and Duration from an audio file's tags. Deliberately does not
/// touch album art — see AlbumArtCacheService for that, even though both
/// classes use TagLib# under the hood.
/// </summary>
public sealed class MetadataReaderService : IMetadataReader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".wav", ".m4a"
    };

    public bool IsSupportedExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
    }

    public async Task<Track> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio file not found.", filePath);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            TagLibFile tagFile;
            try
            {
                tagFile = TagLibFile.Create(filePath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidDataException($"Could not parse tag data for: {filePath}", ex);
            }

            using (tagFile)
            {
                var tag = tagFile.Tag;
                var properties = tagFile.Properties;

                var title = string.IsNullOrWhiteSpace(tag.Title)
                    ? Path.GetFileNameWithoutExtension(filePath)
                    : tag.Title;

                return new Track
                {
                    FilePath = filePath,
                    Title = title,
                    Artist = tag.JoinedPerformers ?? string.Empty,
                    Album = tag.Album ?? string.Empty,
                    Duration = properties?.Duration ?? TimeSpan.Zero,
                    FileModifiedAtUtc = File.GetLastWriteTimeUtc(filePath)
                };
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}