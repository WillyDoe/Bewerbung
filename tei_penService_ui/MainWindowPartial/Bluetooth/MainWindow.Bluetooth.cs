using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using TeiPenServiceConnectionManager.Models;

namespace tei_penService_ui
{
    /// <summary>
    /// Bluetooth-Status, Gerätesuche und zugehörige UI (Indikatoren, Buttons) für MainWindow.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// DependencyProperty für den Bluetooth-Status, um bedingten Hover-Effekt zu ermöglichen.
        /// </summary>
        public static readonly DependencyProperty IsBluetoothEnabledProperty =
            DependencyProperty.Register(nameof(IsBluetoothEnabled), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(true));

        /// <summary>
        /// Gibt an, ob Bluetooth aktiviert ist. Wird für bedingten Hover-Effekt verwendet.
        /// </summary>
        public bool IsBluetoothEnabled
        {
            get => (bool)GetValue(IsBluetoothEnabledProperty);
            set => SetValue(IsBluetoothEnabledProperty, value);
        }

        /// <summary>Verhindert mehrfache Klicks auf den Verbinden-Button während einer laufenden Aktion.</summary>
        private bool _pairDeviceOperationInProgress;
        /// <summary>Verhindert mehrfache Klicks auf den Bluetooth-Button während einer laufenden Aktion.</summary>
        private bool _bluetoothOperationInProgress;

        /// <summary>
        /// Prüft beim Start den initialen Bluetooth-Status und aktualisiert UI-Elemente (Indikator, Gerätebereich, Hover-Brush).
        /// </summary>
        private async Task CheckInitialBluetoothStatusAsync()
        {
            if (_teiPenServiceWrapper == null)
                return;

            try
            {
                bool isEnabled = await _teiPenServiceWrapper.IsBluetoothEnabledAsync();
                await Dispatcher.InvokeAsync(() =>
                {
                    IsBluetoothEnabled = isEnabled;
                    UpdateDeviceSectionVisibility(isEnabled);
                    UpdatePairDeviceButtonHoverBrush();
                    UpdateBluetoothStatusIndicator(isEnabled);
                    UpdatePenConnectedStatusIndicator();
                    UpdateLoggedInStatusIndicator();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Prüfen des initialen Bluetooth-Status: {ex.Message}");
                await Dispatcher.InvokeAsync(() => UpdateBluetoothStatusIndicator(true));
            }
        }

        /// <summary>
        /// Prüft beim Start den initialen Gerätesuche-Status und aktualisiert den Verbinden-Button sowie die Sichtbarkeit des Gerätebereichs.
        /// </summary>
        private async Task CheckInitialDeviceDiscoveryStatusAsync()
        {
            if (_teiPenServiceWrapper == null)
                return;

            try
            {
                bool isActive = await _teiPenServiceWrapper.IsDeviceSearchActiveAsync();
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdatePairDeviceButtonText(isActive);
                    UpdateDeviceSectionVisibility(true);
                    UpdatePairDeviceButtonHoverBrush();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Prüfen des initialen Gerätesuche-Status: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdatePairDeviceButtonText(false);
                    UpdateDeviceSectionVisibility(true);
                    UpdatePairDeviceButtonHoverBrush();
                });
            }
        }

        /// <summary>
        /// Setzt den Text des Verbinden-Buttons: "Gerätesuche beenden" bei aktiver Suche, sonst "tei Pen verbinden".
        /// </summary>
        private void UpdatePairDeviceButtonText(bool isActive)
        {
            var sv = GetStartView();
            if (sv?.PairDeviceButton == null)
                return;
            sv.PairDeviceButton.ButtonText = isActive ? "Gerätesuche beenden" : "tei Pen verbinden";
        }

        /// <summary>
        /// Zeigt oder blendet den Geräte-Tab-Bereich (Verfügbare / Gekoppelte Geräte) abhängig von Bluetooth ein.
        /// </summary>
        private void UpdateDeviceSectionVisibility(bool bluetoothEnabled)
        {
            var sv = GetStartView();
            if (sv?.PenListSectionContainer == null)
                return;
            sv.PenListSectionContainer.Visibility = bluetoothEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (bluetoothEnabled)
                UpdatePenListWidth();
        }

        /// <summary>
        /// Setzt die Hover-Farbe des Verbinden-Buttons: Grün nur wenn Bluetooth an und kein Stift verbunden,
        /// sonst Rot (Bluetooth aus oder bereits eine Verbindung).
        /// </summary>
        private void UpdatePairDeviceButtonHoverBrush()
        {
            var sv = GetStartView();
            if (sv?.PairDeviceButton == null)
                return;

            sv.PairDeviceButton.ButtonTag = IsBluetoothEnabled;

            bool hasConnectedPen = false;
            try
            {
                hasConnectedPen = _teiPenServiceWrapper?.GetConnectedPenInfo() != null;
            }
            catch { /* Wrapper nicht bereit */ }

            bool useGreen = IsBluetoothEnabled && !hasConnectedPen;
            sv.PairDeviceButton.HoverForegroundBrush = (SolidColorBrush)FindResource(useGreen ? "TeiGreenBrush" : "StatusErrorBrush");
        }

        /// <summary>
        /// Event-Handler für Bluetooth-Statusänderung. Aktualisiert Indikator, Hover-Brush und Sichtbarkeit des Gerätebereichs.
        /// </summary>
        private void OnBluetoothStatusChanged(object sender, bool isEnabled)
        {
            IsBluetoothEnabled = isEnabled;
            UpdatePairDeviceButtonHoverBrush();
            UpdateBluetoothStatusIndicator(isEnabled);

            if (!isEnabled)
            {
                ClearDiscoveredPens();
                UpdateDeviceSectionVisibility(false);
            }
            else
            {
                UpdateDeviceSectionVisibility(true);
            }
        }

        /// <summary>
        /// Event-Handler für Änderung des Gerätesuche-Status. Passt die UI an und aktualisiert die Stiftliste bei Suchestart einmalig.
        /// </summary>
        private void OnDeviceDiscoveryStatusChanged(object sender, bool isActive)
        {
            UpdatePairDeviceButtonText(isActive);
            UpdateDeviceSectionVisibility(true);
            UpdatePenListWidth();
            GetStartView()?.UpdatePenListSectionPosition();

            if (isActive)
            {
                _ = UpdateDiscoveredPensAsync();
            }
            else
            {
                PenConnectionInfoModel connectedInfo = null;
                try
                {
                    connectedInfo = _teiPenServiceWrapper?.GetConnectedPenInfo();
                }
                catch { /* Wrapper nicht bereit */ }
                if (connectedInfo != null && !_connectRequestedFromPairedControl)
                    ShowConnectedPenInList(connectedInfo);
                else if (connectedInfo == null && !_connectRequestedFromPairedControl)
                    ClearDiscoveredPens();
                if (_connectRequestedFromPairedControl)
                    _ = Dispatcher.InvokeAsync(() => { _ = UpdatePairedPensAsync(); }, DispatcherPriority.Background);
                else
                    _ = UpdatePairedPensAsync();
            }
        }

        /// <summary>
        /// Aktualisiert die Farbe des Bluetooth-Status-Indikators (TeiGreen bei aktiv, Grau bei deaktiviert) und den Bluetooth-Button-Text.
        /// </summary>
        private void UpdateBluetoothStatusIndicator(bool isEnabled)
        {
            var indicator = FindName("BluetoothStatusIndicator") as Ellipse;
            if (indicator == null)
                return;
            indicator.Fill = GetStatusIndicatorBrush(isEnabled);

            var sv = GetStartView();
            if (sv?.BluetoothButton != null)
                sv.BluetoothButton.ButtonText = isEnabled ? "Bluetooth deaktivieren" : "Bluetooth aktivieren";
        }

        /// <summary>
        /// Behandelt Klick auf den Verbinden-Button. Startet oder beendet die Gerätesuche je nach aktuellem Status.
        /// </summary>
        private async void PairDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_teiPenServiceWrapper == null || _pairDeviceOperationInProgress)
                return;

            _pairDeviceOperationInProgress = true;
            try
            {
                bool isDeviceSearchActive = await _teiPenServiceWrapper.IsDeviceSearchActiveAsync();
                if (isDeviceSearchActive)
                    await _teiPenServiceWrapper.StopDeviceSearchAsync();
                else
                {
                    bool isBluetoothEnabled = await _teiPenServiceWrapper.IsBluetoothEnabledAsync();
                    if (!isBluetoothEnabled)
                        return;
                    await _teiPenServiceWrapper.StartDeviceSearchAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim {(await _teiPenServiceWrapper?.IsDeviceSearchActiveAsync() == true ? "Stoppen" : "Starten")} der Gerätesuche: {ex.Message}");
            }
            finally
            {
                _pairDeviceOperationInProgress = false;
            }
        }

        private SolidColorBrush GetStatusIndicatorBrush(bool isActive)
        {
            return (SolidColorBrush)FindResource(isActive ? "TeiGreenBrush" : "StatusInactiveBrush");
        }

        /// <summary>
        /// Behandelt Klick auf den Bluetooth-Button. Schaltet Bluetooth aktiviert/deaktiviert um.
        /// </summary>
        private async void BluetoothButton_Click(object sender, RoutedEventArgs e)
        {
            if (_teiPenServiceWrapper == null || _bluetoothOperationInProgress)
                return;

            _bluetoothOperationInProgress = true;
            try
            {
                bool currentStatus = await _teiPenServiceWrapper.IsBluetoothEnabledAsync();
                bool success = await _teiPenServiceWrapper.SetBluetoothEnabledAsync(!currentStatus);
                if (!success)
                    Debug.WriteLine($"Fehler beim {(currentStatus ? "Deaktivieren" : "Aktivieren")} von Bluetooth.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Umschalten von Bluetooth: {ex.Message}");
            }
            finally
            {
                _bluetoothOperationInProgress = false;
            }
        }
    }
}
