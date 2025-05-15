using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Brushes = System.Drawing.Brushes;

namespace SimpleV2ray.Views
{
    public class BoolToRunningConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRunning)
            {
                return isRunning ? "正在运行" : "未运行";
            }
            return "未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && value.ToString() == "正在运行";
        }
    }

    public class BoolToRunningColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRunning)
            {
                return isRunning ? Brushes.Green : Brushes.Gray;
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && value == Brushes.Green;
        }
    }
} 