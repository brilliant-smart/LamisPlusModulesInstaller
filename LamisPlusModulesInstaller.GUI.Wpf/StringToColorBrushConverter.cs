using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LamisPlusModulesInstaller.GUI.Wpf
{
    public class StringToColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString && !string.IsNullOrWhiteSpace(colorString))
            {
                try
                {
                    var brush = new BrushConverter().ConvertFrom(colorString);
                    return brush ?? new SolidColorBrush(Colors.Gray);
                }
                catch
                {
                    return new SolidColorBrush(Colors.Gray);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
