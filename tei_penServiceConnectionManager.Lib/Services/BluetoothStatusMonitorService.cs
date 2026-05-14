using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Utilities;
using Windows.Devices.Radios;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Verwaltet die Überwachung des Bluetooth-Status auf dem Host-Gerät.
    /// </summary>
    public class BluetoothStatusMonitorService : IDisposable
    {
        private readonly GenericBluetoothPenClient _bluetoothPenClient;
        private readonly Func<bool> _isDisposedCallback;
        private readonly Action<bool> _onBluetoothStatusChangedCallback;

        // Bluetooth-Status-Überwachung
        private readonly object _bluetoothMonitoringLock = new object();
        private CancellationTokenSource? _bluetoothMonitoringCancellationTokenSource;
        private Task? _bluetoothMonitoringTask;
        private volatile bool _latestBluetoothStatus = true;
        private const int _bluetoothMonitoringInterval = 3000;

        private bool _disposed;

        /// <summary>
        /// Initialisiert den BluetoothStatusMonitor.
        /// </summary>
        /// <param name="bluetoothPenClient">Der GenericBluetoothPenClient für Bluetooth-Status-Checks.</param>
        /// <param name="isDisposedCallback">Callback-Funktion zur Prüfung, ob das übergeordnete Objekt bereits freigegeben wurde.</param>
        /// <param name="onBluetoothStatusChangedCallback">Callback-Funktion, die aufgerufen wird, wenn sich der Bluetooth-Status ändert.</param>
        public BluetoothStatusMonitorService(
            GenericBluetoothPenClient bluetoothPenClient,
            Func<bool> isDisposedCallback,
            Action<bool> onBluetoothStatusChangedCallback)
        {
            _bluetoothPenClient = bluetoothPenClient ?? throw new ArgumentNullException(nameof(bluetoothPenClient));
            _isDisposedCallback = isDisposedCallback ?? throw new ArgumentNullException(nameof(isDisposedCallback));
            _onBluetoothStatusChangedCallback = onBluetoothStatusChangedCallback ?? throw new ArgumentNullException(nameof(onBluetoothStatusChangedCallback));

            // Bluetooth-Status-Überwachung starten
            StartBluetoothMonitoring();
        }

        /// <summary>
        /// Feuert das BluetoothStatusChanged Event thread-safe über Callback.
        /// </summary>
        private void OnBluetoothStatusChanged(bool isEnabled)
        {
            if (_isDisposedCallback())
            {
                return;
            }

            try
            {
                _onBluetoothStatusChangedCallback(isEnabled);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Auslösen des BluetoothStatusChanged Event: {ex.Message}");
            }
        }

        /// <summary>
        /// Prüft periodisch, ob auf dem Host-Gerät Bluetooth aktiviert ist.
        /// Wenn sich der Status ändert, wird das BluetoothStatusChanged Event ausgelöst.
        /// </summary>
        private async Task MonitorHostDeviceBluetoothStatusAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Initialen Bluetooth-Status prüfen
                _latestBluetoothStatus = await IsBluetoothEnabledAsync().ConfigureAwait(false);

                while (!cancellationToken.IsCancellationRequested)
                {
                    bool currentStatus = await IsBluetoothEnabledAsync().ConfigureAwait(false);

                    // Wenn der Status geändert wurde, das Event auslösen
                    if (currentStatus != _latestBluetoothStatus)
                    {
                        _latestBluetoothStatus = currentStatus;
                        OnBluetoothStatusChanged(currentStatus);
                    }

                    // Warte bis zum nächsten Überwachungszyklus + Übergebe CancellationToken
                    await Task.Delay(_bluetoothMonitoringInterval, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Erwartet bei Cancellation, keine Aktion notwendig (Wieso? -> Erklärung später)
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Überwachen des Bluetooth-Status: {ex.Message}!");
            }
        }

        /// <summary>
        /// Startet die Überwachung des Bluetooth-Status auf dem Host-Gerät.
        /// Methode wird im Konstruktor aufgerufen, um die Überwachung des Bluetooth-Status auf dem Host-Gerät zu starten.
        /// Thread-safe.
        /// </summary>
        private void StartBluetoothMonitoring()
        {
            lock (_bluetoothMonitoringLock)
            {
                if (_bluetoothMonitoringTask != null && !_bluetoothMonitoringTask.IsCompleted)
                {
                    return; // Bluetooth Monitoring Task ist bereits aktiv
                }

                _bluetoothMonitoringCancellationTokenSource = new CancellationTokenSource();
                _bluetoothMonitoringTask = Task.Run(async () => await MonitorHostDeviceBluetoothStatusAsync(_bluetoothMonitoringCancellationTokenSource.Token));
            }
        }

        /// <summary>
        /// Stoppt die Überwachung des Bluetooth-Status threadsafe.
        /// </summary>
        public void StopHostDeviceBluetoothMonitoring()
        {
            CancellationTokenSource? ctsToCancel = null;
            Task? taskToWait = null;

            lock (_bluetoothMonitoringLock)
            {
                // Lokale Kopien für Operationen außerhalb des Locks
                ctsToCancel = _bluetoothMonitoringCancellationTokenSource;
                taskToWait = _bluetoothMonitoringTask;

                // Felder zurücksetzen
                _bluetoothMonitoringCancellationTokenSource = null;
                _bluetoothMonitoringTask = null;
            }

            // Operationen außerhalb des Locks ausführen (verhindert Deadlocks)
            if (ctsToCancel != null)
            {
                try
                {
                    ctsToCancel.Cancel();
                    ctsToCancel.Dispose();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Stoppen des Bluetooth-Monitoring CancellationTokenSource: {ex.Message}");
                }
            }

            if (taskToWait != null)
            {
                try
                {
                    // Nicht-blockierendes Warten: Task wird im Hintergrund beendet
                    // Verwende Task.Run um die Warte-Operation nicht zu blockieren
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await taskToWait.ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            ThreadSafeConsole.WriteLine($"Fehler beim Warten auf Bluetooth-Monitoring Task: {ex.Message}");
                        }
                    });
                    
                    // Timeout-Mechanismus: Wenn Task nicht innerhalb von 5 Sekunden beendet wird, wird er ignoriert
                    // Dies verhindert Blockierungen, während der Task normalerweise schnell beendet wird
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        if (!taskToWait.IsCompleted)
                        {
                            ThreadSafeConsole.WriteLine("Bluetooth-Monitoring Task wurde nach 5 Sekunden noch nicht beendet. Wird im Hintergrund fortgesetzt.");
                        }
                    });
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Warten auf Bluetooth-Monitoring Task: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Prüfe, ob auf dem Host-Gerät Bluetooth aktiviert ist.
        /// Wrapped GetBluetoothIsEnabledAsync() aus der SDK-Komponente.
        /// </summary>
        /// <returns>True, wenn Bluetooth aktiviert ist, false sonst.</returns>
        public async Task<bool> IsBluetoothEnabledAsync()
        {
            if (_isDisposedCallback())
            {
                throw new ObjectDisposedException(nameof(BluetoothStatusMonitorService));
            }

            try
            {
                return await _bluetoothPenClient.GetBluetoothIsEnabledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine("Fehler beim Prüfen, ob Bluetooth aktiviert ist: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Aktiviert oder deaktiviert Bluetooth auf dem Host-Gerät.
        /// </summary>
        /// <param name="enabled">True, um Bluetooth zu aktivieren, false um es zu deaktivieren.</param>
        /// <returns>True, wenn die Operation erfolgreich war, false sonst.</returns>
        public async Task<bool> SetBluetoothEnabledAsync(bool enabled)
        {
            if (_isDisposedCallback())
            {
                throw new ObjectDisposedException(nameof(BluetoothStatusMonitorService));
            }

            try
            {
                // Hole alle verfügbaren Radios
                var radios = await Radio.GetRadiosAsync();
                if (radios == null || radios.Count == 0)
                {
                    ThreadSafeConsole.WriteLine("Keine Radios auf dem System gefunden.");
                    return false;
                }

                // Finde das Bluetooth-Radio
                var bluetoothRadio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
                if (bluetoothRadio == null)
                {
                    ThreadSafeConsole.WriteLine("Kein Bluetooth-Radio auf dem System gefunden.");
                    return false;
                }

                // Setze den gewünschten Status
                RadioState targetState = enabled ? RadioState.On : RadioState.Off;
                RadioAccessStatus accessStatus = await bluetoothRadio.SetStateAsync(targetState);

                if (accessStatus == RadioAccessStatus.Allowed)
                {
                    ThreadSafeConsole.WriteLine($"Bluetooth wurde {(enabled ? "aktiviert" : "deaktiviert")}.");
                    return true;
                }
                else
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim {(enabled ? "Aktivieren" : "Deaktivieren")} von Bluetooth. Zugriff verweigert: {accessStatus}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim {(enabled ? "Aktivieren" : "Deaktivieren")} von Bluetooth: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Implementiert die IDisposable-Schnittstelle und gibt alle Ressourcen frei.
        /// Thread-safe.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopHostDeviceBluetoothMonitoring();
        }
    }
}

