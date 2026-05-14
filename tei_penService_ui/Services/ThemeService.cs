using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace tei_penService_ui.Services
{
    /// <summary>
    /// Zentralisierte Theme-Logik: Dictionary-Swap (Light/Dark) und Windows Title Bar (DWM).
    /// </summary>
    public static class ThemeService
    {
        /// <summary>Win32-DWM-Aufruf zum Setzen von Fensterattributen (Dark Mode, Caption-Farbe).</summary>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        /// <summary>DWM-Attribut: Immersive Dark Mode für die Title Bar.</summary>
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private static ResourceDictionary _lightModeDict;
        private static ResourceDictionary _darkModeDict;
        /// <summary>Index des Theme-Dictionary in App.xaml MergedDictionaries (LightMode.xaml = letzter Eintrag, 0-basiert: 5).</summary>
        private const int ThemeSlotIndex = 5;

        /// <summary>
        /// Wendet die Application-Resources für Light oder Dark Mode an (Dictionary-Swap).
        /// </summary>
        public static void ApplyApplicationResources(bool isDarkMode)
        {
            try
            {
                var merged = Application.Current.Resources.MergedDictionaries;
                EnsureThemeDictionariesLoaded(merged);
                merged[ThemeSlotIndex] = isDarkMode ? _darkModeDict : _lightModeDict;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Anwenden des Themes: {ex.Message}");
            }
        }

        private static void EnsureThemeDictionariesLoaded(System.Collections.ObjectModel.Collection<ResourceDictionary> merged)
        {
            if (_darkModeDict != null)
                return;

            _lightModeDict = merged[ThemeSlotIndex];
            _darkModeDict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/tei_penService_ui;component/Assets/Styles/DarkMode.xaml", UriKind.Absolute)
            };
        }

        /// <summary>
        /// Wendet das Theme auf die Windows Title Bar an (Immersive Dark Mode und Caption Color).
        /// </summary>
        public static void ApplyTitleBarTheme(Window window, bool isDarkMode)
        {
            if (window == null)
                return;
            try
            {
                if (PresentationSource.FromVisual(window) == null)
                {
                    window.Loaded += (s, e) => ApplyTitleBarTheme(window, isDarkMode);
                    return;
                }

                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                int darkMode = isDarkMode ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                // Kein DWMWA_CAPTION_COLOR: Bei WindowStyle=None + WindowChrome legt Windows sonst oft
                // eine undurchsichtige Caption-Schicht über die oberen Pixel und blendet WPF-Titelleiste aus.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Anwenden des Title Bar Themes: {ex.Message}");
            }
        }
    }
}
