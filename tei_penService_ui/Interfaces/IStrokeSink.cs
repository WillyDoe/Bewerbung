using System.Collections.Generic;
using System.Windows;

namespace tei_penService_ui.Interfaces
{
    /// <summary>
    /// Interface zum Hinzufügen von Strichen (in Pixelkoordinaten) an eine Anzeige (z. B. InkCanvas).
    /// </summary>
    public interface IStrokeSink
    {
        void AddStroke(IReadOnlyList<Point> points);
    }
}