using System;
using System.Globalization;
using System.Windows.Data;

public class StringArrayToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string[] array)
        {
            return string.Join(", ", array);
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str.Split(new[] { ", " }, StringSplitOptions.None);
        }
        return value;
    }
}
