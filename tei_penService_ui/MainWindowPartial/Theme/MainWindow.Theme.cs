using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using tei_penService_ui.Services;

namespace tei_penService_ui
{
    /// <summary>
    /// Theme-Logik (Light/Dark) und Title Bar für MainWindow.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Gibt an, ob Dark Mode aktiviert ist.
        /// </summary>
        private bool _isDarkMode = false;

        /// <summary>
        /// Wendet das aktuelle Theme (Light oder Dark) an.
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                ThemeService.ApplyApplicationResources(_isDarkMode);

                var titleBarBrush = (SolidColorBrush)Application.Current.Resources["TitleBarBrush"];
                var textBrush = (SolidColorBrush)Application.Current.Resources["TextBrush"];

                if (StatusText != null)
                    StatusText.Foreground = textBrush;

                UpdateButtonTheme(titleBarBrush, textBrush);
                ThemeService.ApplyTitleBarTheme(this, _isDarkMode);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Anwenden des Themes: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualisiert die Buttons für das aktuelle Theme.
        /// </summary>
        private void UpdateButtonTheme(SolidColorBrush backgroundBrush, SolidColorBrush foregroundBrush)
        {
            var sv = GetStartView();
            if (sv == null)
                return;
            if (sv.PairDeviceButton != null)
            {
                sv.PairDeviceButton.BackgroundBrush = backgroundBrush;
                sv.PairDeviceButton.ForegroundBrush = foregroundBrush;
            }
            if (sv.BluetoothButton != null)
            {
                sv.BluetoothButton.BackgroundBrush = backgroundBrush;
                sv.BluetoothButton.ForegroundBrush = foregroundBrush;
            }
            if (sv.StartWithoutPenButton != null)
            {
                sv.StartWithoutPenButton.BackgroundBrush = backgroundBrush;
                sv.StartWithoutPenButton.ForegroundBrush = foregroundBrush;
            }
        }

        /// <summary>
        /// Event Handler für den Dark Mode Toggle Button - Checked (Dark Mode aktivieren).
        /// </summary>
        private void DarkModeToggle_Checked(object sender, RoutedEventArgs e)
        {
            SetDarkMode(true, "Dark Mode");
        }

        /// <summary>
        /// Event Handler für den Dark Mode Toggle Button - Unchecked (Light Mode aktivieren).
        /// </summary>
        private void DarkModeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            SetDarkMode(false, "Light Mode");
        }

        private void SetDarkMode(bool isDarkMode, string modeLabel)
        {
            try
            {
                _isDarkMode = isDarkMode;
                ApplyTheme();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Aktivieren des {modeLabel}: {ex.Message}");
            }
        }
    }
}
