using System.Windows;
using System.Windows.Input;

namespace tei_penService_ui
{
    /// <summary>
    /// Title Bar und Fenster-Buttons (Drag, Minimize, Maximize, Close) für MainWindow.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>Ermöglicht Verschieben des Fensters per Ziehen der Title Bar.</summary>
        private void TitleBarBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        /// <summary>
        /// Minimiert das Fenster.
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = System.Windows.WindowState.Minimized;
        }

        /// <summary>Schaltet zwischen maximiert und normaler Fenstergröße um.</summary>
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == System.Windows.WindowState.Maximized ? System.Windows.WindowState.Normal : System.Windows.WindowState.Maximized;
        }

        /// <summary>
        /// Schließt das Fenster.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
