using System;
using System.Globalization;
using System.Windows.Data;

namespace ClientWPF.Converters
{
    public class BoolToAliveConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAlive)
                return isAlive ? "жив" : "выбыл";
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}