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

            // Show a green check only for Russian UI; English shows no icon for Done.
            if (sk == StatusKind.Done)
            {
                var cur = LocalizationManager.Instance.CurrentCulture ?? "en";
                if (cur.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
                    return "\u2714"; // heavy check mark
                return string.Empty;
            }

            if (sk == StatusKind.Error)
                return "\u2716"; // heavy multiplication x

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
