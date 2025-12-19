using System;
using System.Globalization;
using System.Windows.Data;

namespace ClientWPF.Converters
{
    public class BoolToCurrentPlayerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCurrent && isCurrent)
                return "Ходит";
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}