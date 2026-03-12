using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AutoRegressionVM.Helpers
{
    /// <summary>
    /// null 또는 빈 문자열이면 Collapsed, 값이 있으면 Visible 반환
    /// </summary>
    public class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            if (value is string s && string.IsNullOrWhiteSpace(s))
                return Visibility.Collapsed;

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
