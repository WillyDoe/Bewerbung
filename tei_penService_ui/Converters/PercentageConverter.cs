using System;
using System.Globalization;
using System.Windows.Data;

namespace tei_penService_ui.Converters
{
    /// <summary>
    /// Converter, der einen Wert mit einem Prozentwert (als Parameter) multipliziert.
    /// </summary>
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string percentageStr)
            {
                if (double.TryParse(percentageStr, NumberStyles.Float, culture, out double percentage))
                {
                    return width * (percentage / 100.0);
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
