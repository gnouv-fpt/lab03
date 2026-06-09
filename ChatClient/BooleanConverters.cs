using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ChatClient;

public sealed class OwnMessageAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class OwnMessageBrushConverter : IValueConverter
{
    public Brush OwnBrush { get; set; } = new SolidColorBrush(Color.FromRgb(219, 244, 255));
    public Brush OtherBrush { get; set; } = Brushes.White;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? OwnBrush : OtherBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class OwnMessageForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? new SolidColorBrush(Color.FromRgb(10, 90, 135)) : Brushes.DimGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
