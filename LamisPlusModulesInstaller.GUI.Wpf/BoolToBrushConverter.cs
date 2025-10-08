using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LamisPlusModulesInstaller.GUI.Wpf
{
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? new SolidColorBrush(Colors.SeaGreen) : new SolidColorBrush(Colors.IndianRed);
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
