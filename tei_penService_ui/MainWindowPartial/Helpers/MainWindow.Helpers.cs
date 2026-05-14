using System.Windows;
using System.Windows.Media;
using tei_penService_ui.Controls;

namespace tei_penService_ui
{
    /// <summary>
    /// Hilfsmethoden für MainWindow (z. B. HasConnectedPen).
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Gibt true zurück, wenn mindestens ein Stift verbunden ist (Button zeigt "Starten").
        /// </summary>
        private bool HasConnectedPen()
        {
            var sv = GetStartView();
            if (sv?.PenListContainer == null)
                return false;
            foreach (var item in sv.PenListContainer.Items)
            {
                if (item is PenControlBase pc && pc.IsConnected)
                    return true;
            }
            return false;
        }
    }
}
