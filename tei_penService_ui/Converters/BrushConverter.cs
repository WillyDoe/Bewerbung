using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace tei_penService_ui.Converters
{
    /// <summary>
    /// Converter, der einen Brush zurückgibt, wenn er nicht null ist, sonst einen Standard-Brush
    /// </summary>
    public class BrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Brush brush && brush != null)
            {
                return brush;
            }
            
            // Standard-Brush zurückgeben, wenn parameter als Resource-Key gesetzt ist
            if (parameter is string resourceKey && System.Windows.Application.Current != null)
            {
                var resource = System.Windows.Application.Current.TryFindResource(resourceKey);
                if (resource is Brush defaultBrush)
                {
                    return defaultBrush;
                }
            }
            
            return new SolidColorBrush(Colors.Blue); // Fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
