using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace tei_penService_ui.Converters
{
    /// <summary>
    /// Converter, der einen RSSI-Wert in eine Farbe (Brush) umwandelt.
    /// Rot: RSSI < -70 dBm (schwach)
    /// Gelb: -70 dBm ≤ RSSI < -50 dBm (mittel)
    /// Grün: RSSI ≥ -50 dBm (stark)
    /// </summary>
    public class RssiToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rssi)
            {
                // Farbcodierung basierend auf RSSI-Wert
                if (rssi < -70)
                {
                    // Rot für schwache Verbindung
                    return System.Windows.Application.Current?.TryFindResource("StatusErrorBrush") as Brush 
                        ?? new SolidColorBrush(Color.FromRgb(229, 115, 115)); // #E57373
                }
                else if (rssi < -50)
                {
                    // Gelb für mittlere Verbindung
                    return System.Windows.Application.Current?.TryFindResource("TeiYellowBrush") as Brush 
                        ?? new SolidColorBrush(Color.FromRgb(255, 191, 32)); // #FFBF20
                }
                else
                {
                    // Grün für starke Verbindung
                    return System.Windows.Application.Current?.TryFindResource("TeiGreenBrush") as Brush 
                        ?? new SolidColorBrush(Color.FromRgb(72, 139, 54)); // #488B36
                }
            }

            // Fallback: Grau wenn kein gültiger Wert
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
