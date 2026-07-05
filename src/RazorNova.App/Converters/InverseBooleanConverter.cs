using System.Globalization;
using System.Windows.Data;

namespace RazorNova.App.Converters;

/// <summary>
/// Returns the logical negation of a bound bool — e.g. enabling a button
/// via "{Binding IsScanning, Converter={StaticResource InverseBoolean}}"
/// bound to IsEnabled, without needing a second ObservableProperty just
/// to hold the opposite value.
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value!;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value!;
}