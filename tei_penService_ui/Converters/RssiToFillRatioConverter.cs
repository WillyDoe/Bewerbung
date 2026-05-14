using System;
using System.Globalization;
using System.Windows.Data;

namespace tei_penService_ui.Converters
{
    /// <summary>
    /// Wandelt RSSI (dBm) in einen Füllfaktor 0..1 für die Statusleiste um.
    /// -100 dBm bzw. schlechter = 0, -50 dBm bzw. besser = 1 (linear dazwischen).
    /// Entspricht der gleichen Skala wie RssiToColorConverter (grün ab -50).
    /// </summary>
    public class RssiToFillRatioConverter : IValueConverter
    {
        private const int RssiMin = -100;
        private const int RssiMax = -50;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rssi)
            {
                if (rssi <= RssiMin) return 0.0;
                if (rssi >= RssiMax) return 1.0;
                return (double)(rssi - RssiMin) / (RssiMax - RssiMin);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
