using System;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Shell;
using Neosmartpen.Net.Bluetooth;
using tei_penService_ui.Models;
using tei_penService_ui.Services;
using tei_penService_ui.Views;
using tei_penService_ui.Helpers;

namespace tei_penService_ui
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>Wrapper für TeiPenService (Bluetooth, Gerätesuche, Verbindung).</summary>
        private TeiPenServiceWrapper _teiPenServiceWrapper;
        /// <summary>App-Daten (gekoppelte Stifte, Benutzer); kann null sein, wenn Init fehlschlägt.</summary>
        private AppDataService _appDataService;

        /// <summary>Start-Ansicht mit Verbinden-/Bluetooth-/Starten-Buttons.</summary>
        private StartView _startView;
        /// <summary>Login-/Registrierungs-Overlay.</summary>
        private LoginView _loginView;
        /// <summary>Derzeit angezeigter View im Content-Bereich.</summary>
        private UserControl _currentView;
        /// <summary>Arbeitsbereich-View (nach Start).</summary>
        private WorkspaceView _workspaceView;
        /// <summary>Einstellungs-View.</summary>
        private SettingsView _settingsView;
        /// <summary>Aktuell angemeldeter Benutzer; null wenn nicht angemeldet.</summary>
        private UserMemoryEntry _currentUser = null;

        /// <summary>
        /// Initialisiert das Hauptfenster mit Custom Title Bar, StartView, Event-Handlern und dem Timer für Stiftlisten-Updates.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Custom Title Bar: CaptionHeight 0 — sonst kann DWM (Caption-Layer) die oberen Pixel
            // überdecken und Logo/Text unsichtbar machen. Ziehen erfolgt per TitleBarBorder + DragMove().
            var chrome = new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(4),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);

            // Damit Min/Max/Close, Home und Einstellungen in der Title Bar klickbar sind (Caption-Bereich gibt Hit-Test sonst an Drag weiter)
            this.Loaded += (s, e) =>
            {
                if (FindName("HomeButton") is UIElement homeBtn)
                    WindowChrome.SetIsHitTestVisibleInChrome(homeBtn, true);
                if (FindName("SettingsButton") is UIElement settingsBtn)
                    WindowChrome.SetIsHitTestVisibleInChrome(settingsBtn, true);
                if (FindName("MinimizeButton") is UIElement minBtn)
                    WindowChrome.SetIsHitTestVisibleInChrome(minBtn, true);
                if (FindName("MaximizeButton") is UIElement maxBtn)
                    WindowChrome.SetIsHitTestVisibleInChrome(maxBtn, true);
                if (FindName("CloseButton") is UIElement closeBtn)
                    WindowChrome.SetIsHitTestVisibleInChrome(closeBtn, true);
                if (FindName("StatusBar") is UIElement statusBar)
                    WindowChrome.SetIsHitTestVisibleInChrome(statusBar, true);
            };

            _startView = new StartView();
            _currentView = _startView;

            this.SizeChanged += MainWindow_SizeChanged;
            this.StateChanged += MainWindow_StateChanged;
            this.LocationChanged += MainWindow_LocationChanged;
            this.ContentRendered += MainWindow_ContentRendered;
            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;
            CenterWindowOnScreen();

            // Hover-Farben und Klick-Handler an StartView-Buttons binden
            if (_startView.PairDeviceButton != null)
            {
                _startView.PairDeviceButton.HoverForegroundBrush = (SolidColorBrush)FindResource("TeiGreenBrush");
                _startView.PairDeviceButton.ButtonClick += PairDeviceButton_Click;
            }
            if (_startView.BluetoothButton != null)
            {
                _startView.BluetoothButton.HoverForegroundBrush = (SolidColorBrush)FindResource("TeiCyanBrush");
                _startView.BluetoothButton.ButtonClick += BluetoothButton_Click;
            }
            if (_startView.StartWithoutPenButton != null)
            {
                _startView.StartWithoutPenButton.HoverForegroundBrush = (SolidColorBrush)FindResource("TeiBlueBrush");
                _startView.StartWithoutPenButton.ButtonClick += StartWithoutPenButton_Click;
            }
            _startView.LogoutRequested += OnStartViewLogoutRequested;
        }

        /// <summary>
        /// Reagiert auf Fenstergrößenänderungen und aktualisiert Stiftlistenbereich.
        /// </summary>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            GetStartView()?.UpdatePenListSectionPosition();
            UpdatePenListWidth();
            ApplyMaximizedRootMargin();
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            ApplyMaximizedRootMargin();
        }

        private void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            ApplyMaximizedRootMargin();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            ApplyMaximizedRootMargin();
        }

        /// <summary>
        /// Maximiert + WindowChrome: Nur oben am Root-Grid einziehen (Titelleiste/DWM), links/rechts/unten 0 —
        /// unten kein Rand, damit die Statusleiste bündig am unteren Fensterrand liegt.
        /// </summary>
        private void ApplyMaximizedRootMargin()
        {
            try
            {
                if (MainGrid != null)
                    MainGrid.Margin = new Thickness(0);
                if (TitleBarBorder != null)
                    TitleBarBorder.Margin = new Thickness(0);
                if (ContentChromeInsetHost != null)
                    ContentChromeInsetHost.Margin = new Thickness(0);

                if (WindowState != WindowState.Maximized)
                    return;

                var t = SystemParameters.WindowResizeBorderThickness;
                double topInset = Math.Max(t.Top, 0);

                const double fallbackInset = 8.0;
                if (t.Left < 0.5 && t.Right < 0.5 && t.Top < 0.5 && t.Bottom < 0.5)
                    topInset = fallbackInset;

                Rect work = GetWorkAreaForWindowDips();
                if (Top < work.Top - 0.5)
                    topInset = Math.Max(topInset, work.Top - Top);

                if (MainGrid != null)
                    MainGrid.Margin = new Thickness(0, topInset, 0, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyMaximizedRootMargin: {ex.Message}");
            }
        }

        /// <summary>
        /// Arbeitsbereich des Monitors, auf dem dieses Fenster liegt (DIPs), für korrekten Rand unter Taskleiste / bei Mehrdisplay.
        /// </summary>
        private Rect GetWorkAreaForWindowDips()
        {
            try
            {
                if (MonitorWorkAreaNative.TryGetWorkArea(this, out Rect area))
                    return area;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetWorkAreaForWindowDips: {ex.Message}");
            }

            return SystemParameters.WorkArea;
        }

        private static class MonitorWorkAreaNative
        {
            internal const uint MonitorDefaultToNearest = 2;

            [StructLayout(LayoutKind.Sequential)]
            private struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            private struct MONITORINFO
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public uint dwFlags;
            }

            [DllImport("user32.dll")]
            private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

            internal static bool TryGetWorkArea(Window window, out Rect workAreaDips)
            {
                workAreaDips = SystemParameters.WorkArea;
                if (window == null)
                    return false;

                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return false;

                IntPtr hMon = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                if (hMon == IntPtr.Zero)
                    return false;

                var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (!GetMonitorInfo(hMon, ref mi))
                    return false;

                if (!(PresentationSource.FromVisual(window) is HwndSource source) || source.CompositionTarget == null)
                    return false;

                Matrix fromDevice = source.CompositionTarget.TransformFromDevice;
                Point tl = fromDevice.Transform(new Point(mi.rcWork.Left, mi.rcWork.Top));
                Point br = fromDevice.Transform(new Point(mi.rcWork.Right, mi.rcWork.Bottom));
                workAreaDips = new Rect(tl.X, tl.Y, Math.Max(0, br.X - tl.X), Math.Max(0, br.Y - tl.Y));
                return true;
            }
        }

        /// <summary>
        /// Liefert die StartView-Instanz, wenn derzeit der Start-View angezeigt wird.
        /// </summary>
        private StartView GetStartView()
        {
            return _currentView as StartView;
        }

        /// <summary>
        /// Liefert das ContentControl für den wechselbaren View-Bereich (x:Name="MainContentHost").
        /// </summary>
        private ContentControl GetMainContentHost()
        {
            return FindName("MainContentHost") as ContentControl;
        }

        /// <summary>
        /// Wird beim Laden des Fensters aufgerufen. Initialisiert Views, TeiPenServiceWrapper, Events und prüft Bluetooth- sowie Gerätesuche-Status.
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var host = GetMainContentHost();
            if (host != null)
                host.Content = _startView;
            UpdateHomeButtonVisibility();
            UpdateStartViewLoggedInState();

            // Fenster nochmals zentrieren, um sicherzustellen, dass die Position korrekt ist
            CenterWindowOnScreen();

            // Position und Breite der Tab-Leiste/Stiftliste unter der Button-Zeile setzen (nach Layout)
            GetStartView()?.UpdatePenListSectionPosition();
            UpdatePenListWidth();

            // Theme initial anwenden (inkl. TextBrush)
            ApplyTheme();

            ApplyMaximizedRootMargin();

            try
            {
                // AppDataService instanziieren und initialisieren
                _appDataService = new AppDataService();
                await _appDataService.InitializeAsync();
                await _appDataService.SyncPairedPensFromLibAsync();


                // LoginView erstellen und konfigurieren
                _loginView = new LoginView();
                if (_appDataService != null)
                    _loginView.SetAppDataService(_appDataService);
                _loginView.LoginSucceeded += OnLoginSucceeded;
                _loginView.CloseRequested += OnLoginCloseRequested;

                // TeiPenServiceWrapper instanziieren
                _teiPenServiceWrapper = new TeiPenServiceWrapper(Dispatcher);

                // Events abonnieren
                _teiPenServiceWrapper.BluetoothStatusChanged += OnBluetoothStatusChanged;
                _teiPenServiceWrapper.DeviceDiscoveryStatusChanged += OnDeviceDiscoveryStatusChanged;
                _teiPenServiceWrapper.PenDiscovered += OnPenDiscovered;
                _teiPenServiceWrapper.PenRemoved += OnPenRemoved;
                _teiPenServiceWrapper.PenUpdated += OnPenUpdated;
                _teiPenServiceWrapper.ConnectionStatusChanged += OnWrapperConnectionStatusChanged;
                _teiPenServiceWrapper.PasswordRequired += OnWrapperPasswordRequired;

                // TeiPenService im Background initialisieren
                await _teiPenServiceWrapper.InitializeAsync();

                // Gekoppelte Stifte laden und anzeigen
                _ = UpdatePairedPensAsync();

                // Initialen Bluetooth-Status prüfen
                await CheckInitialBluetoothStatusAsync();

                // Initialen Gerätesuche-Status prüfen
                await CheckInitialDeviceDiscoveryStatusAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler bei der Initialisierung des TeiPenServiceWrapper: {ex.Message}");
                // Bei Fehler Standard-Farbe (TeiCyan) verwenden
                UpdateBluetoothStatusIndicator(true);
            }
        }

        /// <summary>
        /// Wird beim Schließen des Fensters aufgerufen. Führt Cleanup (Event-Unsubscription, Dispose) durch.
        /// </summary>
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (_teiPenServiceWrapper != null)
            {
                _teiPenServiceWrapper.BluetoothStatusChanged -= OnBluetoothStatusChanged;
                _teiPenServiceWrapper.DeviceDiscoveryStatusChanged -= OnDeviceDiscoveryStatusChanged;
                _teiPenServiceWrapper.PenDiscovered -= OnPenDiscovered;
                _teiPenServiceWrapper.PenRemoved -= OnPenRemoved;
                _teiPenServiceWrapper.PenUpdated -= OnPenUpdated;
                _teiPenServiceWrapper.ConnectionStatusChanged -= OnWrapperConnectionStatusChanged;
                _teiPenServiceWrapper.PasswordRequired -= OnWrapperPasswordRequired;
                _teiPenServiceWrapper.Dispose();
                _teiPenServiceWrapper = null;
            }
        }

        /// <summary>
        /// Passt die Breite der Stiftliste automatisch an die Breite der Button-Row an.
        /// </summary>
        private void UpdatePenListWidth()
        {
            GetStartView()?.UpdatePenListWidth();
        }

        /// <summary>
        /// Positioniert das Fenster in der absoluten Mitte des Hauptbildschirms.
        /// </summary>
        private void CenterWindowOnScreen()
        {
            // Hauptbildschirm-Auflösung abrufen
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Fenster-Größe abrufen (falls noch nicht geladen, verwende definierte Größe)
            double windowWidth = this.Width > 0 ? this.Width : 1200;
            double windowHeight = this.Height > 0 ? this.Height : 800;

            // Position berechnen: Mitte des Bildschirms minus halbe Fenster-Größe
            double left = (screenWidth - windowWidth) / 2;
            double top = (screenHeight - windowHeight) / 2;

            // Fenster-Position setzen
            this.Left = left;
            this.Top = top;
        }

        // PenList-Methoden in MainWindow.PenList.cs

    }
}
