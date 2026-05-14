using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Utilities;
using Windows.Devices.Bluetooth;
using Windows.Foundation;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Verwaltet die Suche nach Bluetooth-Pens sowie die Überwachung während der Discovery-Phase.
    /// Entfernt Pens event-basiert (RSSI außer Reichweite &gt; 8 s) und zeitbasiert (kein Discovery-Update &gt; 12 s).
    /// </summary>
    public partial class PenDiscoveryService : IDisposable
    {

        private readonly GenericBluetoothPenClient _discoveryPenClient;
        private readonly Func<bool> _isDisposedCallback;
        private readonly Action<PenInformation> _addPenToReachCallback;
        private readonly Func<ConcurrentDictionary<string, PenInformation>> _getPensInReachCallback;
        private readonly Action<string> _removePenFromReachCallback;
        private readonly Func<ConcurrentDictionary<string, DateTime?>> _getPenOutOfRangeTimestampsCallback;
        private readonly Action<string, DateTime?> _updatePenOutOfRangeTimestampCallback;
        private readonly Action<string, DateTime> _updatePenLastUpdateTimestampCallback;
        private readonly Func<ConcurrentDictionary<string, DateTime>> _getPenLastUpdateTimestampsCallback;
        private readonly Func<string, bool> _isPenConnectedCallback;
        private readonly Action<bool>? _onDeviceDiscoveryStatusChangedCallback;
        private readonly Action<PenInformation>? _notifyPenUpdatedCallback;

        // Thread-safe Flag für aktive Gerätesuche
        private readonly object _isSearchingLock = new object();
        private bool _isSearching;

        // Timeout für Out-of-Range-Erkennung (in Sekunden)
        private const int _outOfRangeTimeoutSeconds = 20;

        // Time-based No-Update-Monitoring (integriert aus PenConnectionMonitoringService)
        private readonly object _noUpdateMonitoringLock = new object();
        private CancellationTokenSource? _noUpdateMonitoringCancellationTokenSource;
        private Task? _noUpdateMonitoringTask;
        private const int _noUpdateTimeoutSeconds = 20;
        private const int _noUpdateMonitoringIntervalMs = 2000;

        private bool _disposed;
        /// <summary>
        /// Initialisiert den PenDiscoveryService.
        /// </summary>
        /// <param name="discoveryPenClient">Der GenericBluetoothPenClient für Discovery.</param>
        /// <param name="isDisposedCallback">Callback-Funktion zur Prüfung, ob das übergeordnete Objekt bereits freigegeben wurde.</param>
        /// <param name="addPenToReachCallback">Callback-Funktion zum Hinzufügen eines Stifts zur Liste der Pens in Reichweite.</param>
        /// <param name="getPensInReachCallback">Callback-Funktion zum Abrufen des Pens in Reichweite Dictionary.</param>
        /// <param name="removePenFromReachCallback">Callback-Funktion zum Entfernen eines Stifts aus der Liste der Pens in Reichweite.</param>
        /// <param name="getPenOutOfRangeTimestampsCallback">Callback-Funktion zum Abrufen des Out-of-Range-Timestamps Dictionary.</param>
        /// <param name="updatePenOutOfRangeTimestampCallback">Callback-Funktion zum Aktualisieren des Out-of-Range-Timestamps für einen Stift.</param>
        /// <param name="updatePenLastUpdateTimestampCallback">Callback-Funktion zum Aktualisieren des Last Update Timestamps für einen Stift.</param>
        /// <param name="getPenLastUpdateTimestampsCallback">Callback-Funktion zum Abrufen des Last Update Timestamps Dictionary (für No-Update-Monitoring).</param>
        /// <param name="isPenConnectedCallback">Callback-Funktion zur Prüfung, ob ein Stift verbunden ist.</param>
        /// <param name="onDeviceDiscoveryStatusChangedCallback">Optionaler Callback, der aufgerufen wird, wenn sich der Gerätesuche-Status ändert (true = aktiv, false = gestoppt).</param>
        /// <param name="notifyPenUpdatedCallback">Optionaler Callback, der aufgerufen wird, wenn ein Stift (z. B. RSSI) aktualisiert wurde.</param>
        public PenDiscoveryService(
            GenericBluetoothPenClient discoveryPenClient,
            Func<bool> isDisposedCallback,
            Action<PenInformation> addPenToReachCallback,
            Func<ConcurrentDictionary<string, PenInformation>> getPensInReachCallback,
            Action<string> removePenFromReachCallback,
            Func<ConcurrentDictionary<string, DateTime?>> getPenOutOfRangeTimestampsCallback,
            Action<string, DateTime?> updatePenOutOfRangeTimestampCallback,
            Action<string, DateTime> updatePenLastUpdateTimestampCallback,
            Func<ConcurrentDictionary<string, DateTime>> getPenLastUpdateTimestampsCallback,
            Func<string, bool> isPenConnectedCallback,
            Action<bool>? onDeviceDiscoveryStatusChangedCallback = null,
            Action<PenInformation>? notifyPenUpdatedCallback = null)
        {
            _discoveryPenClient = discoveryPenClient ?? throw new ArgumentNullException(nameof(discoveryPenClient));
            _isDisposedCallback = isDisposedCallback ?? throw new ArgumentNullException(nameof(isDisposedCallback));
            _addPenToReachCallback = addPenToReachCallback ?? throw new ArgumentNullException(nameof(addPenToReachCallback));
            _getPensInReachCallback = getPensInReachCallback ?? throw new ArgumentNullException(nameof(getPensInReachCallback));
            _removePenFromReachCallback = removePenFromReachCallback ?? throw new ArgumentNullException(nameof(removePenFromReachCallback));
            _getPenOutOfRangeTimestampsCallback = getPenOutOfRangeTimestampsCallback ?? throw new ArgumentNullException(nameof(getPenOutOfRangeTimestampsCallback));
            _updatePenOutOfRangeTimestampCallback = updatePenOutOfRangeTimestampCallback ?? throw new ArgumentNullException(nameof(updatePenOutOfRangeTimestampCallback));
            _updatePenLastUpdateTimestampCallback = updatePenLastUpdateTimestampCallback ?? throw new ArgumentNullException(nameof(updatePenLastUpdateTimestampCallback));
            _getPenLastUpdateTimestampsCallback = getPenLastUpdateTimestampsCallback ?? throw new ArgumentNullException(nameof(getPenLastUpdateTimestampsCallback));
            _isPenConnectedCallback = isPenConnectedCallback ?? throw new ArgumentNullException(nameof(isPenConnectedCallback));
            _onDeviceDiscoveryStatusChangedCallback = onDeviceDiscoveryStatusChangedCallback;
            _notifyPenUpdatedCallback = notifyPenUpdatedCallback;
        }

        /// <summary>
        /// Wirft eine ObjectDisposedException, wenn das übergeordnete Objekt bereits freigegeben wurde.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposedCallback())
            {
                throw new ObjectDisposedException(nameof(PenDiscoveryService));
            }
        }

        /// <summary>
        /// Startet die kontinuierliche, nicht blockierende Suche nach Bluetooth-Pens im Hintergrund.
        /// </summary>
        public void StartDeviceDiscovery()
        {
            ThrowIfDisposed();

            lock (_isSearchingLock)
            {
                if (_isSearching)
                {
                    ThreadSafeConsole.WriteLine("Gerätesuche ist bereits aktiv.");
                    return;
                }
                // Events abonnieren
                SubscribeToSDKBluetoothEvents();

                // Watcher starten
                _discoveryPenClient.StartLEAdvertisementWatcher();
                _isSearching = true;
            }

            ThreadSafeConsole.WriteLine("Gerätesuche gestartet...");
            
            // Status-Änderung melden
            _onDeviceDiscoveryStatusChangedCallback?.Invoke(true);

            // Time-based No-Update-Monitoring starten
            StartNoUpdateMonitoring();
        }

        /// <summary>
        /// Stoppt die kontinuierliche Suche nach Bluetooth-Pens.
        /// </summary>
        public void StopDeviceDiscovery()
        {
            ThrowIfDisposed();

            // Time-based No-Update-Monitoring zuerst stoppen
            StopNoUpdateMonitoring();

            lock (_isSearchingLock)
            {
                if (!_isSearching)
                {
                    ThreadSafeConsole.WriteLine("Gerätesuche ist nicht aktiv.");
                    return;
                }

                // Watcher stoppen
                _discoveryPenClient.StopLEAdvertisementWatcher();
                _isSearching = false;

                // Events abbestellen
                UnsubscribeFromSDKBluetoothEvents();
            }

            ThreadSafeConsole.WriteLine("Gerätesuche gestoppt.");
            
            // Status-Änderung melden
            _onDeviceDiscoveryStatusChangedCallback?.Invoke(false);
        }

        /// <summary>
        /// Prüft, ob die Gerätesuche aktuell aktiv ist.
        /// Thread-safe: Verwendet lock für sicheren Zugriff auf _isSearching.
        /// </summary>
        /// <returns>True, wenn die Gerätesuche aktiv ist, false sonst.</returns>
        public bool IsDeviceDiscoveryActive()
        {
            ThrowIfDisposed();

            lock (_isSearchingLock)
            {
                return _isSearching;
            }
        }

        /// <summary>
        /// Abonniert die Windows Bluetooth Discovery Events.
        /// Wird automatisch beim StartDeviceDiscovery() aufgerufen.
        /// </summary>
        private void SubscribeToSDKBluetoothEvents()
        {
            _discoveryPenClient.onAddPenController += OnDeviceInReach;
            _discoveryPenClient.onUpdatePenController += OnDeviceInReachUpdated;
            _discoveryPenClient.onStopSearch += OnSearchStopped;
        }

        /// <summary>
        /// Deabonniert die Windows Bluetooth Discovery Events.
        /// Wird automatisch beim StopDeviceDiscovery(), OnSearchStopped(), Dispose() aufgerufen.
        /// </summary>
        private void UnsubscribeFromSDKBluetoothEvents()
        {
            _discoveryPenClient.onAddPenController -= OnDeviceInReach;
            _discoveryPenClient.onUpdatePenController -= OnDeviceInReachUpdated;
            _discoveryPenClient.onStopSearch -= OnSearchStopped;
        }

        /// <summary>
        /// Startet das zeitbasierte No-Update-Monitoring (prüft periodisch, ob Stifte zu lange keine Discovery-Updates mehr hatten).
        /// </summary>
        private void StartNoUpdateMonitoring()
        {
            ThrowIfDisposed();

            lock (_noUpdateMonitoringLock)
            {
                if (_noUpdateMonitoringTask != null && !_noUpdateMonitoringTask.IsCompleted)
                {
                    return;
                }

                _noUpdateMonitoringCancellationTokenSource = new CancellationTokenSource();
                _noUpdateMonitoringTask = Task.Run(async () => await MonitorPensAsync(_noUpdateMonitoringCancellationTokenSource.Token));
            }
        }

        /// <summary>
        /// Stoppt das zeitbasierte No-Update-Monitoring.
        /// </summary>
        private void StopNoUpdateMonitoring()
        {
            CancellationTokenSource? ctsToCancel = null;
            Task? taskToWait = null;

            lock (_noUpdateMonitoringLock)
            {
                ctsToCancel = _noUpdateMonitoringCancellationTokenSource;
                taskToWait = _noUpdateMonitoringTask;
                _noUpdateMonitoringCancellationTokenSource = null;
                _noUpdateMonitoringTask = null;
            }

            if (ctsToCancel != null)
            {
                try
                {
                    ctsToCancel.Cancel();
                    ctsToCancel.Dispose();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Stoppen des No-Update-Monitoring CancellationTokenSource: {ex.Message}");
                }
            }

            if (taskToWait != null)
            {
                try
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await taskToWait.ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            ThreadSafeConsole.WriteLine($"Fehler beim Warten auf No-Update-Monitoring Task: {ex.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Stoppen des No-Update-Monitoring Tasks: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Prüft periodisch, ob Stifte zu lange keine Discovery-Events mehr gesendet haben.
        /// Entfernt Stifte, die länger als den Timeout keine Updates mehr gesendet haben. Verbundene Stifte werden nicht entfernt.
        /// </summary>
        private async Task MonitorPensAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_isDisposedCallback())
                    {
                        return;
                    }

                    var pensInReach = _getPensInReachCallback();
                    var lastUpdateTimestamps = _getPenLastUpdateTimestampsCallback();
                    DateTime now = DateTime.UtcNow;

                    foreach (var kvp in pensInReach)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        string macAddress = kvp.Key;

                        if (_isPenConnectedCallback(macAddress))
                        {
                            continue;
                        }

                        if (!lastUpdateTimestamps.TryGetValue(macAddress, out DateTime lastUpdate))
                        {
                            _removePenFromReachCallback(macAddress);
                            continue;
                        }

                        TimeSpan timeSinceLastUpdate = now - lastUpdate;

                        if (timeSinceLastUpdate.TotalSeconds >= _noUpdateTimeoutSeconds)
                        {
                            _removePenFromReachCallback(macAddress);
                        }
                    }

                    await Task.Delay(_noUpdateMonitoringIntervalMs, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Erwartet bei Cancellation
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Überwachen der Stifte: {ex.Message}");
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

            StopNoUpdateMonitoring();

            lock (_isSearchingLock)
            {
                if (_isSearching)
                {
                    try
                    {
                        _discoveryPenClient.StopLEAdvertisementWatcher();
                        _isSearching = false;
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine("Fehler beim Stoppen der Gerätesuche: " + ex.Message);
                    }
                }
            }

            try
            {
                UnsubscribeFromSDKBluetoothEvents();
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine("Fehler beim Abbestellen der Events: " + ex.Message);
            }
        }
    }
}

