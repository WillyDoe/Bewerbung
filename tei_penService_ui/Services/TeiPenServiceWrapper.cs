using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Neosmartpen.Net;
using System.Threading.Tasks;
using System.Windows.Threading;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Services;
using tei_penService_ui.Interfaces;
using tei_penService_ui.Models;

namespace tei_penService_ui.Services
{
    /// <summary>
    /// Wrapper-Service für TeiPenService, der Thread-Marshalling handhabt
    /// und sicherstellt, dass Library-Aufrufe die UI nicht blockieren.
    /// </summary>
    public class TeiPenServiceWrapper : ITeiPenServiceWrapper
    {
        /// <summary>Instanz der TeiPen-Bibliothek (Bluetooth, Gerätesuche, Verbindung).</summary>
        private TeiPenService _teiPenService;
        /// <summary>Dispatcher für Marshalling von Events auf den UI-Thread.</summary>
        private readonly Dispatcher _dispatcher;
        /// <summary>Lock für thread-sicheres Dispose.</summary>
        private readonly object _disposeLock = new object();
        /// <summary>True, wenn Dispose aufgerufen wurde.</summary>
        private bool _disposed;

        /// <summary>
        /// Event, das ausgelöst wird, wenn der Bluetooth-Status geändert wird.
        /// Wird asynchron auf dem UI-Thread gemarshalled.
        /// </summary>
        public event EventHandler<bool> BluetoothStatusChanged;

        /// <summary>
        /// Event, das ausgelöst wird, wenn sich der Gerätesuche-Status geändert hat.
        /// Wird asynchron auf dem UI-Thread gemarshalled.
        /// </summary>
        public event EventHandler<bool> DeviceDiscoveryStatusChanged;

        /// <summary>
        /// Event, das ausgelöst wird, wenn ein Stift während der Gerätesuche gefunden wurde.
        /// Wird asynchron auf dem UI-Thread gemarshalled.
        /// </summary>
        public event EventHandler<PenInformation> PenDiscovered;

        /// <summary>
        /// Event, das ausgelöst wird, wenn ein Stift aus der Liste der Pens in Reichweite entfernt wurde.
        /// Wird asynchron auf dem UI-Thread gemarshalled.
        /// </summary>
        public event EventHandler<PenInformation> PenRemoved;

        /// <summary>
        /// Event, das ausgelöst wird, wenn sich die Daten eines Stifts in Reichweite geändert haben (z. B. RSSI).
        /// Wird asynchron auf dem UI-Thread gemarshalled.
        /// </summary>
        public event EventHandler<PenInformation> PenUpdated;

        /// <summary>
        /// Event, das ausgelöst wird, wenn sich der Verbindungsstatus zu einem Stift geändert hat.
        /// Wird asynchron auf dem UI-Thread gemarshalled.
        /// </summary>
        public event EventHandler<bool> ConnectionStatusChanged;

        /// <summary>
        /// Event, das ausgelöst wird, wenn ein Passwort für die Verbindung benötigt wird.
        /// Wird asynchron auf dem UI-Thread gemarshalled.
        /// </summary>
        public event EventHandler<string> PasswordRequired;

        /// <summary>
        /// Event, das ausgelöst wird, wenn ein Dot empfangen wurde.
        /// Wird asynchron auf dem UI-Thread gemarshalled.
        /// </summary>
        public event EventHandler<DotReceivedEventArgs> DotReceived;

        /// <inheritdoc />
        public event EventHandler<PenStrokeCompletedEventArgs> PenStrokeCompleted;

        /// <inheritdoc />
        public event EventHandler<RecognizedTextEventArgs> RecognizedTextAvailable;

        /// <summary>
        /// Initialisiert eine neue Instanz des TeiPenServiceWrapper.
        /// </summary>
        /// <param name="dispatcher">Der Dispatcher für Thread-Marshalling (normalerweise Application.Current.Dispatcher).</param>
        public TeiPenServiceWrapper(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _disposed = false;
        }

        /// <summary>
        /// Initialisiert den TeiPenService im Background-Task.
        /// Verhindert, dass die UI während der Initialisierung blockiert wird.
        /// </summary>
        public async Task InitializeAsync()
        {
            ThrowIfDisposed();

            try
            {
                // TeiPenService im Background-Task initialisieren
                _teiPenService = await Task.Run(() => new TeiPenService());

                // Event-Subscription auf UI-Thread
                await _dispatcher.InvokeAsync(() =>
                {
                    if (_teiPenService != null)
                    {
                        _teiPenService.BluetoothStatusChanged += OnBluetoothStatusChanged;
                        _teiPenService.DeviceDiscoveryStatusChanged += OnDeviceDiscoveryStatusChanged;
                        _teiPenService.PenDiscovered += OnPenDiscovered;
                        _teiPenService.PenRemoved += OnPenRemoved;
                        _teiPenService.PenUpdated += OnPenUpdated;
                        _teiPenService.ConnectionStatusChanged += OnConnectionStatusChanged;
                        _teiPenService.PasswordRequired += OnPasswordRequired;
                        _teiPenService.DotReceived += OnDotReceived;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler bei der Initialisierung des TeiPenService: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Startet die Suche nach tei Pens.
        /// </summary>
        /// <returns>True, wenn die Suche erfolgreich gestartet wurde, false sonst.</returns>
        public async Task<bool> StartDeviceSearchAsync()
        {
            ThrowIfDisposed();

            // prüfen, ob TeiPenService initialisiert ist
            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            // prüfen, ob Bluetooth aktiviert ist
            bool isBluetoothEnabled = await IsBluetoothEnabledAsync();
            if (!isBluetoothEnabled)
            {
                throw new InvalidOperationException("Bluetooth ist nicht aktiviert. Biten Sie aktivieren Sie Bluetooth, um die Suche nach tei Pens zu starten.");
            }

            try
            {
                // Gerätesuche starten - im background thread ausführen
                // obwohl synchron, für Konsistenz und Thread-Marshalling verwenden
                await Task.Run(() => _teiPenService.StartDeviceDiscovery());

                return true;  // Suche wurde erfolgreich gestartet 
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Starten der Gerätesuche: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stoppt die Suche nach tei Pens.
        /// </summary>
        /// <returns>True, wenn die Suche erfolgreich gestoppt wurde, false sonst.</returns>
        public async Task<bool> StopDeviceSearchAsync()
        {
            ThrowIfDisposed();

            // prüfen, ob TeiPenService initialisiert ist
            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            try
            {
                // Gerätesuche stoppen - im background thread ausführen
                // obwohl synchron, für Konsistenz und Thread-Marshalling verwenden
                await Task.Run(() => _teiPenService.StopDeviceDiscovery());

                return true;  // Suche wurde erfolgreich gestoppt 
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Stoppen der Gerätesuche: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Prüft, ob die Gerätesuche aktuell aktiv ist.
        /// Thread-safe und non-blocking: Führt die Prüfung in einem Background-Thread aus.
        /// </summary>
        /// <returns>True, wenn die Gerätesuche aktiv ist, false sonst.</returns>
        public async Task<bool> IsDeviceSearchActiveAsync()
        {
            ThrowIfDisposed();

            // prüfen, ob TeiPenService initialisiert ist
            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            try
            {
                // Gerätesuche-Status prüfen - im background thread ausführen
                // für Konsistenz und Thread-Marshalling verwenden
                return await Task.Run(() => _teiPenService.IsDeviceDiscoveryActive());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Prüfen des Gerätesuche-Status: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ruft alle gefundenen Stifte ab.
        /// Thread-safe und non-blocking: Führt die Abfrage in einem Background-Thread aus.
        /// </summary>
        /// <returns>Eine Collection der gefundenen Stifte.</returns>
        public async Task<IReadOnlyCollection<PenInformation>> GetDiscoveredPensAsync()
        {
            ThrowIfDisposed();

            // prüfen, ob TeiPenService initialisiert ist
            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            try
            {
                // Gefundene Stifte abrufen - im background thread ausführen
                // für Konsistenz und Thread-Marshalling verwenden
                return await Task.Run(() => _teiPenService.GetDiscoveredPens());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Abrufen der gefundenen Stifte: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Prüft, ob auf dem Host-Gerät Bluetooth aktiviert ist.
        /// </summary>
        /// <returns>True, wenn Bluetooth aktiviert ist, false sonst.</returns>
        public async Task<bool> IsBluetoothEnabledAsync()
        {
            ThrowIfDisposed();

            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            try
            {
                return await _teiPenService.IsBluetoothEnabledAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Prüfen des Bluetooth-Status: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Aktiviert oder deaktiviert Bluetooth auf dem Host-Gerät.
        /// </summary>
        /// <param name="enabled">True, um Bluetooth zu aktivieren, false um es zu deaktivieren.</param>
        /// <returns>True, wenn die Operation erfolgreich war, false sonst.</returns>
        public async Task<bool> SetBluetoothEnabledAsync(bool enabled)
        {
            ThrowIfDisposed();

            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            try
            {
                return await _teiPenService.SetBluetoothEnabledAsync(enabled);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim {(enabled ? "Aktivieren" : "Deaktivieren")} von Bluetooth: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Verbindet mit einem Smartpen (1:1 Verbindung).
        /// Führt den Lib-Call im Hintergrund aus, um die UI nicht zu blockieren.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Smartpens.</param>
        /// <returns>True, wenn die Verbindung erfolgreich hergestellt wurde, false sonst.</returns>
        public async Task<bool> ConnectToPenAsync(string macAddress)
        {
            ThrowIfDisposed();

            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            try
            {
                return await _teiPenService.ConnectToPenAsync(macAddress).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Verbinden mit Stift {macAddress}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gibt die Verbindungsinformationen des verbundenen Smartpens zurück.
        /// </summary>
        /// <returns>Verbindungsinformationen oder null, wenn kein Stift verbunden ist.</returns>
        public PenConnectionInfoModel GetConnectedPenInfo()
        {
            ThrowIfDisposed();

            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            return _teiPenService.GetConnectedPenInfo();
        }

        /// <summary>
        /// Prüft, ob ein Stift mit der angegebenen MAC-Adresse verbunden ist.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts.</param>
        /// <returns>True, wenn der Stift verbunden ist, false sonst.</returns>
        public bool IsPenConnected(string macAddress)
        {
            ThrowIfDisposed();

            if (_teiPenService == null)
            {
                return false;
            }

            return _teiPenService.IsPenConnected(macAddress);
        }

        /// <summary>
        /// Gibt das Passwort manuell ein, wenn der Stift danach fragt.
        /// </summary>
        /// <param name="password">Das einzugebende Passwort.</param>
        /// <returns>True, wenn das Passwort erfolgreich eingegeben wurde, false sonst.</returns>
        public bool InputPassword(string password)
        {
            ThrowIfDisposed();

            if (_teiPenService == null)
            {
                return false;
            }

            return _teiPenService.InputPassword(password ?? string.Empty);
        }

        /// <summary>
        /// Setzt das Passwort des verbundenen Stifts (nur wenn ein Stift verbunden ist).
        /// </summary>
        /// <param name="oldPassword">Aktuelles Passwort (oder leer, wenn noch keins gesetzt).</param>
        /// <param name="newPassword">Neues Passwort.</param>
        /// <returns>True wenn erfolgreich, sonst false.</returns>
        public async Task<bool> SetPasswordAsync(string oldPassword, string newPassword)
        {
            ThrowIfDisposed();
            if (_teiPenService == null)
                return false;
            try
            {
                return await _teiPenService.SetPasswordAsync(oldPassword ?? string.Empty, newPassword ?? string.Empty).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Setzen des Stift-Passworts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Setzt den Bluetooth Local Name des verbundenen Stifts (Lib: SetDisplayNameAsync).
        /// Nur anwendbar, wenn der Stift verbunden ist; max. 16 Zeichen.
        /// </summary>
        /// <param name="displayName">Der neue Anzeigename (max. 16 Zeichen für das Gerät).</param>
        /// <returns>True wenn erfolgreich, sonst false.</returns>
        public async Task<bool> SetDisplayNameAsync(string displayName)
        {
            ThrowIfDisposed();
            if (_teiPenService == null)
                return false;
            try
            {
                return await _teiPenService.SetDisplayNameAsync(displayName ?? string.Empty).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Setzen des Stift-Anzeigenamens (Bluetooth): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Trennt die Verbindung zum Smartpen.
        /// Führt den Lib-Call im Hintergrund aus, um die UI nicht zu blockieren.
        /// </summary>
        /// <returns>True, wenn die Verbindung erfolgreich getrennt wurde, false sonst.</returns>
        public async Task<bool> DisconnectFromPenAsync()
        {
            ThrowIfDisposed();

            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            try
            {
                return await _teiPenService.DisconnectFromPenAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Trennen der Verbindung: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gibt alle gekoppelten Stifte aus der memory.json zurück.
        /// </summary>
        public async Task<Dictionary<string, PenMemoryEntry>> GetPairedPensAsync()
        {
            ThrowIfDisposed();

            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            try
            {
                return await _teiPenService.GetPairedPensAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Abrufen der gekoppelten Stifte: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Entfernt einen gekoppelten Stift aus der memory.json.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts.</param>
        /// <returns>True, wenn der Stift entfernt wurde, false sonst.</returns>
        public async Task<bool> RemovePairedPenAsync(string macAddress)
        {
            ThrowIfDisposed();

            if (_teiPenService == null)
            {
                throw new InvalidOperationException("TeiPenService wurde noch nicht initialisiert. Rufen Sie InitializeAsync() auf.");
            }

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            try
            {
                return await _teiPenService.RemovePairedPenAsync(macAddress).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Entfernen des gekoppelten Stifts {macAddress}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Event-Handler für BluetoothStatusChanged aus TeiPenService.
        /// Marshalled das Event asynchron auf den UI-Thread.
        /// </summary>
        private void OnBluetoothStatusChanged(object sender, bool isEnabled)
        {
            if (_disposed)
            {
                return;
            }

            // Event asynchron auf UI-Thread marshallen (nicht-blockierend)
            _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    BluetoothStatusChanged?.Invoke(this, isEnabled);
                }
            });
        }

        /// <summary>
        /// Event-Handler für DeviceDiscoveryStatusChanged aus TeiPenService.
        /// Marshalled das Event asynchron auf den UI-Thread.
        /// </summary>
        private void OnDeviceDiscoveryStatusChanged(object sender, bool isActive)
        {
            if (_disposed)
            {
                return;
            }

            // Event asynchron auf UI-Thread marshallen (nicht-blockierend)
            _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    DeviceDiscoveryStatusChanged?.Invoke(this, isActive);
                }
            });
        }

        /// <summary>
        /// Event-Handler für PenDiscovered aus TeiPenService.
        /// Marshalled das Event asynchron auf den UI-Thread.
        /// </summary>
        private void OnPenDiscovered(object sender, PenInformation penInformation)
        {
            if (_disposed)
            {
                return;
            }

            // Event asynchron auf UI-Thread marshallen (nicht-blockierend)
            _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    PenDiscovered?.Invoke(this, penInformation);
                }
            });
        }

        /// <summary>
        /// Event-Handler für PenRemoved aus TeiPenService.
        /// Marshalled das Event asynchron auf den UI-Thread.
        /// </summary>
        private void OnPenRemoved(object sender, PenInformation penInformation)
        {
            if (_disposed)
            {
                return;
            }

            // Event asynchron auf UI-Thread marshallen (nicht-blockierend)
            _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    PenRemoved?.Invoke(this, penInformation);
                }
            });
        }

        /// <summary>
        /// Event-Handler für PenUpdated aus TeiPenService.
        /// Marshalled das Event asynchron auf den UI-Thread.
        /// </summary>
        private void OnPenUpdated(object sender, PenInformation penInformation)
        {
            if (_disposed)
            {
                return;
            }

            _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    PenUpdated?.Invoke(this, penInformation);
                }
            });
        }

        /// <summary>
        /// Event-Handler für ConnectionStatusChanged aus TeiPenService.
        /// Marshalled das Event asynchron auf den UI-Thread.
        /// </summary>
        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            if (_disposed)
            {
                return;
            }

            _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    ConnectionStatusChanged?.Invoke(this, isConnected);
                }
            });
        }

        /// <summary>
        /// Event-Handler für PasswordRequired aus TeiPenService.
        /// Marshalled das Event asynchron auf den UI-Thread.
        /// </summary>
        private void OnPasswordRequired(object sender, string macAddress)
        {
            if (_disposed)
            {
                return;
            }

            _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed)
                {
                    PasswordRequired?.Invoke(this, macAddress);
                }
            });
        }

        /// <summary>
        /// Event-Handler für DotReceived aus TeiPenService.
        /// Synchron auf den UI-Thread marshallen, damit PEN_UP/CompleteCurrentStroke strikt vor dem
        /// nächsten PEN_DOWN verarbeitet wird.
        /// </summary>
        private void OnDotReceived(object sender, DotReceivedEventArgs args)
        {
            if (_disposed || args == null)
                return;
            try
            {
                _dispatcher.Invoke(() =>
                {
                    if (!_disposed)
                        DotReceived?.Invoke(this, args);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TeiPenServiceWrapper.OnDotReceived UI-Marshal: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void PublishPenStrokeCompleted(IReadOnlyList<Point> points)
        {
            ThrowIfDisposed();
            if (points == null || points.Count == 0)
                return;
            var args = new PenStrokeCompletedEventArgs(points);
            void Raise() => PenStrokeCompleted?.Invoke(this, args);
            try
            {
                if (_dispatcher.CheckAccess())
                    Raise();
                else
                    _dispatcher.Invoke(Raise);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TeiPenServiceWrapper.PublishPenStrokeCompleted: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void PublishRecognizedText(string text, string source)
        {
            ThrowIfDisposed();
            var args = new RecognizedTextEventArgs(text, source);
            void Raise() => RecognizedTextAvailable?.Invoke(this, args);
            try
            {
                if (_dispatcher.CheckAccess())
                    Raise();
                else
                    _dispatcher.Invoke(Raise);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TeiPenServiceWrapper.PublishRecognizedText: {ex.Message}");
            }
        }

        /// <summary>
        /// Prüft, ob das Objekt bereits freigegeben wurde.
        /// </summary>
        private bool IsDisposed()
        {
            lock (_disposeLock)
            {
                return _disposed;
            }
        }

        /// <summary>
        /// Wirft eine ObjectDisposedException, wenn das Objekt bereits freigegeben wurde.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (IsDisposed())
            {
                throw new ObjectDisposedException(nameof(TeiPenServiceWrapper));
            }
        }

        /// <summary>
        /// Gibt alle Ressourcen frei.
        /// </summary>
        public void Dispose()
        {
            bool shouldDispose = false;
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                shouldDispose = true;
            }

            if (shouldDispose)
            {
                try
                {
                    // Event-Unsubscription auf UI-Thread (asynchron für Robustheit)
                    // Verwende InvokeAsync mit Timeout, um Deadlocks zu vermeiden
                    var disposeOperation = _dispatcher.InvokeAsync(() =>
                    {
                        if (_teiPenService != null)
                        {
                            _teiPenService.BluetoothStatusChanged -= OnBluetoothStatusChanged;
                            _teiPenService.DeviceDiscoveryStatusChanged -= OnDeviceDiscoveryStatusChanged;
                            _teiPenService.PenDiscovered -= OnPenDiscovered;
                            _teiPenService.PenRemoved -= OnPenRemoved;
                            _teiPenService.PenUpdated -= OnPenUpdated;
                            _teiPenService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                            _teiPenService.PasswordRequired -= OnPasswordRequired;
                            _teiPenService.DotReceived -= OnDotReceived;
                            _teiPenService.Dispose();
                            _teiPenService = null;
                        }
                    });

                    // Warte auf Completion mit Timeout (5 Sekunden)
                    var status = disposeOperation.Wait(TimeSpan.FromSeconds(5));
                    if (status != System.Windows.Threading.DispatcherOperationStatus.Completed)
                    {
                        Debug.WriteLine($"Warnung: Dispose-Operation Status: {status}. Führe Cleanup direkt aus.");
                        // Fallback: Direktes Cleanup wenn Dispatcher nicht antwortet
                        if (_teiPenService != null)
                        {
                            try
                            {
                                _teiPenService.BluetoothStatusChanged -= OnBluetoothStatusChanged;
                                _teiPenService.DeviceDiscoveryStatusChanged -= OnDeviceDiscoveryStatusChanged;
                                _teiPenService.PenDiscovered -= OnPenDiscovered;
                                _teiPenService.PenRemoved -= OnPenRemoved;
                                _teiPenService.PenUpdated -= OnPenUpdated;
                                _teiPenService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                                _teiPenService.PasswordRequired -= OnPasswordRequired;
                                _teiPenService.DotReceived -= OnDotReceived;
                                _teiPenService.Dispose();
                            }
                            catch (Exception fallbackEx)
                            {
                                Debug.WriteLine($"Fehler beim Fallback-Dispose: {fallbackEx.Message}");
                            }
                            _teiPenService = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Fehler beim Disposen des TeiPenServiceWrapper: {ex.Message}");
                    // Fallback: Versuche direktes Cleanup
                    try
                    {
                        if (_teiPenService != null)
                        {
                            _teiPenService.BluetoothStatusChanged -= OnBluetoothStatusChanged;
                            _teiPenService.DeviceDiscoveryStatusChanged -= OnDeviceDiscoveryStatusChanged;
                            _teiPenService.PenDiscovered -= OnPenDiscovered;
                            _teiPenService.PenRemoved -= OnPenRemoved;
                            _teiPenService.PenUpdated -= OnPenUpdated;
                            _teiPenService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                            _teiPenService.PasswordRequired -= OnPasswordRequired;
                            _teiPenService.DotReceived -= OnDotReceived;
                            _teiPenService.Dispose();
                            _teiPenService = null;
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        Debug.WriteLine($"Fehler beim Fallback-Dispose: {fallbackEx.Message}");
                    }
                }
            }
        }
    }
}
