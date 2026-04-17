// AgBoundaryTool
// Converter for FlagColor to Avalonia Brush

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AgBoundaryTool.Models;

namespace AgBoundaryTool.Converters;

public class FlagColorConverter : IValueConverter
{
    public static readonly FlagColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FlagColor color)
        {
            var hex = Flag.ColorToHex(color);
            return Brush.Parse(hex);
        }

        return Brushes.Red;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
