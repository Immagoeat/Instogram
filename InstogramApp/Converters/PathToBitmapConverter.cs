using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace InstogramApp.Converters;

public class PathToBitmapConverter : IValueConverter
{
    public static readonly PathToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path)) return null;
        try { return File.Exists(path) ? new Bitmap(path) : null; }
        catch { return null; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
