using System.Globalization;
using System.Windows.Data;

namespace RazorNova.App.Converters;

/// <summary>
/// Formats a TimeSpan as "m:ss" (or "h:mm:ss" past one hour) — used for
/// the Duration column in the library/playlist track lists. Kept as a
/// converter (rather than a pre-formatted string on Track itself) so
/// Core.Models.Track stays a plain, display-agnostic data model.
/// </summary>
public sealed class TimeSpanToShortStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan time || time < TimeSpan.Zero)
            return "0:00";

        return time.Hours > 0 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(TimeSpanToShortStringConverter)} only supports one-way binding.");
}