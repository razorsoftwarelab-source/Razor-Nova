using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace RazorNova.App.Converters;

/// <summary>
/// Converts raw image bytes (from AlbumArtResult.ImageData) into a WPF
/// ImageSource for binding to an Image control's Source property. Used
/// for album art everywhere in the UI (track list, player controls, mini
/// player). Returns null on any decode failure rather than throwing, so
/// a single corrupt cover image can never crash data binding.
/// </summary>
public sealed class ByteArrayToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
            return null;

        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad; // decodes immediately, so the MemoryStream can be safely disposed right after
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze(); // makes it safely usable across threads/bindings
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(ByteArrayToImageSourceConverter)} only supports one-way binding.");
}