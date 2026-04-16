using System;
using System.Globalization;
using System.Windows.Data;
using PokerLogsViewer.ViewModels;
using PokerLogsViewer.Services;

namespace PokerLogsViewer.Converters
{
    public class StatusKindToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not StatusKind sk) return string.Empty;

            // Show a check mark for Done in all languages.
            if (sk == StatusKind.Done)
            {
                return "\u2714"; // heavy check mark
            }

            if (sk == StatusKind.Error)
                return "\u2716"; // heavy multiplication x

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
