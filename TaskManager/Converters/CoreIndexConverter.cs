using System;
using System.Globalization;
using System.Windows.Data;

namespace TaskManager.Converters
{
    public class CoreIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return $"Ядро {index}";
            }
            return "Ядро";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}