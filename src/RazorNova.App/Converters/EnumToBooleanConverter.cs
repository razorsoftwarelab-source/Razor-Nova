using System.Globalization;
using System.Windows.Data;

namespace RazorNova.App.Converters;

/// <summary>
/// Generic two-way converter for binding a single enum-valued property
/// to several RadioButtons — one per enum member — without writing a
/// dedicated converter per enum type. ConverterParameter is the target
/// enum member's name as a string (e.g. "Night", "All").
/// Usage: IsChecked="{Binding SelectedTheme, Converter={StaticResource EnumToBoolean}, ConverterParameter=Night}"
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string targetName) return false;
        return string.Equals(value.ToString(), targetName, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Only act on the "being checked" notification — the companion
        // "being unchecked" notification for the previously selected
        // RadioButton would otherwise overwrite the new value.
        if (value is not true || parameter is not string targetName) return Binding.DoNothing;
        return Enum.Parse(targetType, targetName);
    }
}