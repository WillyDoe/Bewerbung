using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Utilities;
using TeiPenServiceConnectionManager.Services;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Haupt-Service-Klasse für die Verwaltung von Bluetooth Pen-Verbindungen (1:1 Standalone)
    /// Integration aller Komponenten: Connection Management, Data Transfer, Data
    /// </summary>
    public partial class TeiPenService : IDisposable
    {
        /// SDK-Komponenten
        /// Discovery Controller
        private readonly PenController _discoveryPenController;
        private readonly GenericBluetoothPenClient _discoveryPenClient;

        // Manager-Komponenten
        private readonly BluetoothStatusMonitorService _bluetoothStatusMonitor;
        private readonly PenDiscoveryService _penDiscoveryService;

        // Single Pen Connection (1:1)
        private readonly PenController _penController;
        private readonly GenericBluetoothPenClient _penClient;
        private readonly PenConnectionInfoModel _connectionInfo;

        // Memory Service für persistente Speicherung
        private readonly MemoryService _memoryService;

        // Gefundene Pens in Reichweite (zentrale Verwaltung)
        private readonly ConcurrentDictionary<string, PenInformation> _pensInReach;

        // Out-of-Range-Timestamps für Discovery-Monitoring (Key = MAC-Adresse, Value = DateTime? wenn außer Reichweite, null wenn in Reichweite)
        private readonly ConcurrentDictionary<string, DateTime?> _penOutOfRangeTimestamps;

        // Last Update Timestamps für Time-based Monitoring (Key = MAC-Adresse, Value = DateTime des letzten Discovery-Event-Updates)
        private readonly ConcurrentDictionary<string, DateTime> _penLastUpdateTimestamps;

        // Single Pen Connection Manager
        private SinglePenConnectionManager? _singlePenConnectionManager;

        // Thread-safe Dispose-Flag
        private readonly object _disposeLock = new object();
        private bool _disposed;

        /// <summary>
        /// Event, das ausgelöst wird, wenn der Bluetooth-Status geändert wird.
        /// Parameter: (sender, isEnabled)
        /// </summary>
        public event EventHandler<bool>? BluetoothStatusChanged;

        /// <summary>
        /// Event, das ausgelöst wird, wenn sich der Verbindungsstatus zu einem Stift geändert hat.
        /// Parameter: (sender, isConnected) - true wenn verbunden, false wenn getrennt
        /// </summary>
        public event EventHandler<bool>? ConnectionStatusChanged;

        /// <summary>
        /// Event, das ausgelöst wird, wenn ein Passwort für die Verbindung benötigt wird.
        /// Parameter: (sender, macAddress) - MAC-Adresse des Stifts, der das Passwort benötigt
        /// </summary>
        public event EventHandler<string>? PasswordRequired;

        /// <summary>
        /// Event, das ausgelöst wird, wenn sich der Gerätesuche-Status geändert hat.
        /// Parameter: (sender, isActive) - true wenn Gerätesuche aktiv, false wenn gestoppt
        /// </summary>
        public event EventHandler<bool>? DeviceDiscoveryStatusChanged;

        /// <summary>
        /// Event, das ausgelöst wird, wenn ein Stift während der Gerätesuche gefunden wurde.
        /// Parameter: (sender, penInformation) - Informationen des gefundenen Stifts
        /// </summary>
        public event EventHandler<PenInformation>? PenDiscovered;

        /// <summary>
        /// Event, das ausgelöst wird, wenn ein Stift aus der Liste der Pens in Reichweite entfernt wurde.
        /// Parameter: (sender, penInformation) - Informationen des entfernten Stifts
        /// </summary>
        public event EventHandler<PenInformation>? PenRemoved;

        /// <summary>
        /// Event, das ausgelöst wird, wenn sich die Daten eines Stifts in Reichweite geändert haben (z. B. RSSI).
        /// Parameter: (sender, penInformation) - aktualisierte Informationen des Stifts
        /// </summary>
        public event EventHandler<PenInformation>? PenUpdated;

        /// <summary>
        /// Event, das ausgelöst wird, wenn ein Dot empfangen wurde.
        /// Parameter: (sender, dotArgs) - Informationen zum empfangenen Dot
        /// </summary>
        public event EventHandler<DotReceivedEventArgs>? DotReceived;

        /// <summary>
        /// Initialisiert den TeiPenService mit den Core SDK-Komponenten.
        /// </summary>
        public TeiPenService()
        {
            _discoveryPenController = new PenController();
            _discoveryPenClient = new GenericBluetoothPenClient(_discoveryPenController);
            _disposed = false;

            // SDK-Komponenten für einzelne Verbindung
            _penController = new PenController();
            _penClient = new GenericBluetoothPenClient(_penController);
            _connectionInfo = new PenConnectionInfoModel();

            // Pens in Reichweite Dictionary initialisieren
            _pensInReach = new ConcurrentDictionary<string, PenInformation>();

            // Out-of-Range-Timestamps Dictionary initialisieren
            _penOutOfRangeTimestamps = new ConcurrentDictionary<string, DateTime?>();

            // Last Update Timestamps Dictionary initialisieren
            _penLastUpdateTimestamps = new ConcurrentDictionary<string, DateTime>();

            // Manager-Instanzen erstellen
            _bluetoothStatusMonitor = new BluetoothStatusMonitorService(
                _discoveryPenClient,
                IsDisposed,
                OnBluetoothStatusChanged);

            // PenDiscoveryService mit Callbacks für Pens-Verwaltung und Monitoring erstellen
            _penDiscoveryService = new PenDiscoveryService(
                _discoveryPenClient,
                IsDisposed,
                (PenInformation penInformation) => AddPenToReach(penInformation),
                () => GetPensInReachDictionary(),
                (macAddress) => RemovePenFromReach(macAddress),
                () => GetPenOutOfRangeTimestampsDictionary(),
                (macAddress, timestamp) => UpdatePenOutOfRangeTimestamp(macAddress, timestamp),
                (macAddress, timestamp) => UpdatePenLastUpdateTimestamp(macAddress, timestamp),
                () => GetPenLastUpdateTimestampsDictionary(),
                IsPenConnected,
                OnDeviceDiscoveryStatusChanged,
                (PenInformation penInformation) => OnPenUpdated(penInformation));

            // Memory Service erstellen und im Hintergrund initialisieren
            _memoryService = new MemoryService();
            // InitializeAsync im Hintergrund aufrufen (Fire-and-Forget mit Fehlerbehandlung)
            Task.Run(async () =>
            {
                try
                {
                    await _memoryService.InitializeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler bei der Initialisierung des MemoryService: {ex.Message}");
                }
            });

            // SinglePenConnectionManager wird lazy in ConnectToPenAsync erstellt
        }

        /// <summary>
        /// Startet die kontinuierliche, nicht blockierende Suche nach Bluetooth-Pens im Hintergrund.
        /// </summary>
        public void StartDeviceDiscovery()
        {
            ThrowIfDisposed();
            _penDiscoveryService.StartDeviceDiscovery();
        }

        /// <summary>
        /// Stoppt die kontinuierliche Suche nach Bluetooth-Pens.
        /// </summary>
        public void StopDeviceDiscovery()
        {
            ThrowIfDisposed();
            _penDiscoveryService.StopDeviceDiscovery();
            ClearDiscoveryData();
        }

        /// <summary>
        /// Prüft, ob die Gerätesuche aktuell aktiv ist.
        /// Thread-safe: Delegiert an PenDiscoveryService, welches Lock-Mechanismen verwendet.
        /// </summary>
        /// <returns>True, wenn die Gerätesuche aktiv ist, false sonst.</returns>
        public bool IsDeviceDiscoveryActive()
        {
            ThrowIfDisposed();
            return _penDiscoveryService.IsDeviceDiscoveryActive();
        }

        /// <summary>
        /// Gibt alle gefunden Pens zurück.
        /// Thread-safe: Erstellt einen Snapshot der gefundenen Pens als Array,
        /// um Race Conditions zu vermeiden, falls die Liste während der Iteration verändert wird.
        /// </summary>
        /// <returns>Ein readonly Collection der gefundenen Pens.</returns>
        public IReadOnlyCollection<PenInformation> GetDiscoveredPens()
        {
            return _pensInReach.Values.ToArray();
        }

        /// <summary>
        /// Gibt die Pens in Reichweite Dictionary zurück (für interne Services).
        /// </summary>
        internal ConcurrentDictionary<string, PenInformation> GetPensInReachDictionary()
        {
            return _pensInReach;
        }

        /// <summary>
        /// Gibt die Out-of-Range-Timestamps Dictionary zurück (für interne Services).
        /// Wird von PenDiscoveryService für event-basiertes Discovery-Monitoring verwendet.
        /// </summary>
        internal ConcurrentDictionary<string, DateTime?> GetPenOutOfRangeTimestampsDictionary()
        {
            return _penOutOfRangeTimestamps;
        }

        /// <summary>
        /// Aktualisiert den Out-of-Range-Timestamp für einen Stift.
        /// Wird von PenDiscoveryService aufgerufen, wenn ein Stift außer/in Reichweite ist.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (normalisiert).</param>
        /// <param name="timestamp">Timestamp wenn außer Reichweite, null wenn in Reichweite.</param>
        internal void UpdatePenOutOfRangeTimestamp(string macAddress, DateTime? timestamp)
        {
            if (string.IsNullOrEmpty(macAddress))
            {
                return;
            }

            string normalizedMacAddress = macAddress.ToUpperInvariant();
            _penOutOfRangeTimestamps.AddOrUpdate(normalizedMacAddress, timestamp, (key, oldValue) => timestamp);
        }

        /// <summary>
        /// Gibt die Last Update Timestamps Dictionary zurück (für interne Services).
        /// Wird von PenDiscoveryService für time-based No-Update-Monitoring verwendet.
        /// </summary>
        internal ConcurrentDictionary<string, DateTime> GetPenLastUpdateTimestampsDictionary()
        {
            return _penLastUpdateTimestamps;
        }

        /// <summary>
        /// Aktualisiert den Last Update Timestamp für einen Stift.
        /// Wird von PenDiscoveryService aufgerufen, wenn ein Discovery-Event-Update kommt.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (normalisiert).</param>
        /// <param name="timestamp">Timestamp des letzten Discovery-Event-Updates.</param>
        internal void UpdatePenLastUpdateTimestamp(string macAddress, DateTime timestamp)
        {
            if (string.IsNullOrEmpty(macAddress))
            {
                return;
            }

            string normalizedMacAddress = macAddress.ToUpperInvariant();
            _penLastUpdateTimestamps.AddOrUpdate(normalizedMacAddress, timestamp, (key, oldValue) => timestamp);
        }

        /// <summary>
        /// Entfernt einen Stift aus der Liste der Pens in Reichweite.
        /// Thread-safe.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts.</param>
        /// <returns>True, wenn der Stift entfernt wurde, false wenn nicht gefunden.</returns>
        internal bool RemovePenFromReach(string macAddress)
        {
            if (string.IsNullOrEmpty(macAddress))
            {
                return false;
            }

            // MAC-Adresse normalisieren für Dictionary-Lookup
            string normalizedMacAddress = macAddress.ToUpperInvariant();

            if (_pensInReach.TryRemove(normalizedMacAddress, out PenInformation removedPen))
            {
                // Out-of-Range-Timestamp ebenfalls entfernen
                _penOutOfRangeTimestamps.TryRemove(normalizedMacAddress, out _);
                // Last Update Timestamp ebenfalls entfernen
                _penLastUpdateTimestamps.TryRemove(normalizedMacAddress, out _);
                ThreadSafeConsole.WriteLine($"tei-Pen {normalizedMacAddress} aus Pens in Reichweite entfernt.");

                // Event feuern, wenn Stift erfolgreich entfernt wurde
                OnPenRemoved(removedPen);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Setzt die Discovery-Daten zurück (z. B. bei Beendigung der Gerätesuche oder Bluetooth aus).
        /// Leert _pensInReach, _penOutOfRangeTimestamps und _penLastUpdateTimestamps und feuert PenRemoved für jeden zuvor gefundenen Stift.
        /// </summary>
        private void ClearDiscoveryData()
        {
            if (IsDisposed())
            {
                return;
            }

            PenInformation[] snapshot = _pensInReach.Values.ToArray();
            _pensInReach.Clear();
            _penOutOfRangeTimestamps.Clear();
            _penLastUpdateTimestamps.Clear();

            foreach (PenInformation pen in snapshot)
            {
                try
                {
                    OnPenRemoved(pen);
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Auslösen von PenRemoved beim Zurücksetzen der Discovery-Daten: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Fügt einen Stift zur Liste der Pens in Reichweite hinzu.
        /// Thread-safe.
        /// </summary>
        /// <param name="penInformation">Die PenInformation des Stifts.</param>
        /// <returns>True, wenn der Stift hinzugefügt wurde, false wenn bereits vorhanden.</returns>
        internal bool AddPenToReach(PenInformation penInformation)
        {
            if (penInformation == null)
            {
                return false;
            }

            // MAC-Adresse als primärer Identifier verwenden
            string? macAddress = penInformation.MacAddress;
            if (string.IsNullOrEmpty(macAddress))
            {
                macAddress = penInformation.Id;
                if (string.IsNullOrEmpty(macAddress))
                {
                    return false;
                }
            }

            // MAC-Adresse normalisieren (großgeschrieben) für konsistente Dictionary-Keys
            macAddress = macAddress.ToUpperInvariant();

            // Gerät thread-safe in _pensInReach hinzufügen oder aktualisieren (Key = MAC-Adresse, normalisiert)
            // Hinweis: RSSI-Wert wird später durch Discovery-Events (OnDeviceInReachUpdated) aktualisiert, wenn Updates kommen
            bool wasAdded = _pensInReach.TryAdd(macAddress, penInformation);

            if (!wasAdded)
            {
                // Stift bereits vorhanden - aktualisieren
                _pensInReach[macAddress] = penInformation;
            }

            // Out-of-Range-Timestamp initialisieren (null = in Reichweite)
            _penOutOfRangeTimestamps.TryAdd(macAddress, null);

            // Last Update Timestamp initialisieren (aktueller Zeitpunkt)
            _penLastUpdateTimestamps.TryAdd(macAddress, DateTime.UtcNow);

            // Event feuern, wenn Stift erfolgreich hinzugefügt wurde
            OnPenDiscovered(penInformation);

            return true;
        }

        /// <summary>
        /// Gibt alle bereits gekoppelten tei-Pens in der memory.json an.
        /// </summary>
        /// <returns>Ein readonly Collection der gekoppelten tei-Pens.</returns>
        public async Task<Dictionary<string, PenMemoryEntry>> GetPairedPensAsync()
        {
            return await _memoryService.GetPairedPensAsync();
        }

        /// <summary>
        /// Entfernt einen bestimmten gekoppelten tei-Pen aus der memory.json.
        /// Trennt automatisch die Verbindung, falls der Stift aktuell verbunden ist.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts.</param>
        /// <returns>True, wenn der Stift entfernt wurde, false wenn nicht gefunden.</returns>
        public async Task<bool> RemovePairedPenAsync(string macAddress)
        {
            ThrowIfDisposed();

            // Prüfen, ob der Stift aktuell verbunden ist
            if (IsPenConnected(macAddress))
            {
                ThreadSafeConsole.WriteLine($"Stift {macAddress} ist aktuell verbunden. Verbindung wird getrennt...");
                await DisconnectFromPenAsync().ConfigureAwait(false);
            }

            return await _memoryService.RemovePairedPenAsync(macAddress).ConfigureAwait(false);
        }

        /// <summary>
        /// Entfernt alle gekoppelten tei-Pens aus der memory.json.
        /// Trennt automatisch die Verbindung, falls ein Stift aktuell verbunden ist.
        /// </summary>
        public async Task RemoveAllPairedPensAsync()
        {
            ThrowIfDisposed();

            // Prüfen, ob aktuell ein Stift verbunden ist und Verbindung trennen
            if (_singlePenConnectionManager != null && _singlePenConnectionManager.HasActiveConnection)
            {
                var connectedPen = GetConnectedPenInfo();
                if (connectedPen != null && !string.IsNullOrEmpty(connectedPen.MacAddress))
                {
                    ThreadSafeConsole.WriteLine($"Stift {connectedPen.MacAddress} ist aktuell verbunden. Verbindung wird getrennt...");
                    await DisconnectFromPenAsync().ConfigureAwait(false);
                }
            }

            await _memoryService.RemoveAllPairedPensAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Gibt die Verbindungsinformationen des verbundenen Smartpens zurück.
        /// </summary>
        /// <returns>Verbindungsinformationen des verbundenen Smartpens oder null, wenn kein Smartpen verbunden ist.</returns>
        public PenConnectionInfoModel? GetConnectedPenInfo()
        {
            return _singlePenConnectionManager?.GetConnectedPenInfo();
        }

        /// <summary>
        /// Gibt den aktuellen SinglePenConnectionManager zurück.
        /// </summary>
        private SinglePenConnectionManager? GetConnectionManager()
        {
            return _singlePenConnectionManager;
        }

        /// <summary>
        /// Gibt den ConnectionManager zurück, wenn eine aktive Verbindung besteht. Sonst wird eine Meldung ausgegeben und null zurückgegeben.
        /// </summary>
        private SinglePenConnectionManager? GetConnectionManagerOrLog()
        {
            var cm = _singlePenConnectionManager;
            if (cm == null || !cm.HasActiveConnection)
            {
                ThreadSafeConsole.WriteLine("Kein Stift verbunden. Bitte zuerst eine Verbindung herstellen.");
                return null;
            }
            return cm;
        }

        /// <summary>
        /// Verbindet mit einem Smartpen (1:1 Verbindung).
        /// Verwendet ausschließlich die MAC-Adresse als Identifier.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Smartpens (z.B. "9C:7B:D2:1A:19:E6").</param>
        /// <returns>True, wenn die Verbindung erfolgreich hergestellt wurde, false sonst.</returns>
        public async Task<bool> ConnectToPenAsync(string macAddress)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            var pensInReach = GetPensInReachDictionary();

            // MAC-Adresse normalisieren (großgeschrieben) für Dictionary-Lookup
            string normalizedMacAddress = macAddress.ToUpperInvariant();

            // Suche nach MAC-Adresse
            if (!pensInReach.TryGetValue(normalizedMacAddress, out PenInformation? penInfo))
            {
                ThreadSafeConsole.WriteLine($"Stift mit MAC-Adresse '{macAddress}' nicht gefunden.");
                return false;
            }

            // Prüfen, ob bereits verbunden
            if (_singlePenConnectionManager != null && _singlePenConnectionManager.HasActiveConnection)
            {
                ThreadSafeConsole.WriteLine($"Stift {macAddress} bereits verbunden.");
                return true;
            }

            // Bestehende Verbindung trennen, falls vorhanden
            if (_singlePenConnectionManager != null)
            {
                try
                {
                    await _singlePenConnectionManager.DisconnectAsync().ConfigureAwait(false);
                    _singlePenConnectionManager.Dispose();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Trennen der bestehenden Verbindung: {ex.Message}");
                }
            }

            // Neue Verbindung herstellen
            try
            {
                // Callback für das Speichern im Memory
                async Task SavePenToMemoryAsync()
                {
                    try
                    {
                        await _memoryService.AddOrUpdatePenAsync(
                            normalizedMacAddress,
                            _connectionInfo.DeviceId,
                            _connectionInfo.PenName,
                            _connectionInfo.DisplayName,
                            _connectionInfo.Protocol).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Warnung: Stift konnte nicht in memory.json gespeichert werden: {ex.Message}");
                    }
                }

                // Callback für Passwort-Anforderung
                void OnPasswordRequiredCallback(string macAddr)
                {
                    try
                    {
                        PasswordRequired?.Invoke(this, macAddr);
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Fehler beim Auslösen des PasswordRequired Events: {ex.Message}");
                    }
                }

                // Prüfen, ob Stift bereits bekannt ist (für automatische Verbindung)
                bool isKnown = await _memoryService.IsPenKnownAsync(normalizedMacAddress).ConfigureAwait(false);
                if (isKnown)
                {
                    _connectionInfo.WasAutoConnected = true;
                }
                else
                {
                    _connectionInfo.WasAutoConnected = false;
                }

                _singlePenConnectionManager = new SinglePenConnectionManager(
                    (IPenController)_penController,
                    _penClient,
                    _connectionInfo,
                    SavePenToMemoryAsync,
                    null,
                    _memoryService,
                    () => OnConnectionStatusChanged(true),
                    () => OnConnectionStatusChanged(false),
                    OnPasswordRequiredCallback,
                    null,
                    OnDotReceivedCallback); // dataStorageService wird intern erstellt

                bool connected = await _singlePenConnectionManager.ConnectAsync(penInfo).ConfigureAwait(false);

                if (connected)
                {
                    ThreadSafeConsole.WriteLine($"Physische Verbindung zu Stift {macAddress} hergestellt. Warte auf Authentifizierung...");

                    // Prüfen, ob der Stift noch in der Liste "Pens in Reichweite" ist
                    if (!pensInReach.ContainsKey(normalizedMacAddress))
                    {
                        AddPenToReach(penInfo);
                        ThreadSafeConsole.WriteLine($"Stift {macAddress} wurde wieder zur Liste 'Pens in Reichweite' hinzugefügt.");
                    }
                }
                else
                {
                    ThreadSafeConsole.WriteLine($"Stift {macAddress} Verbindung fehlgeschlagen.");
                }

                return connected;
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Verbinden mit Stift {macAddress}: {ex.Message}");
                _singlePenConnectionManager?.Dispose();
                _singlePenConnectionManager = null;
                return false;
            }
        }

        /// <summary>
        /// Trennt die Verbindung zum Smartpen.
        /// </summary>
        /// <returns>True, wenn die Verbindung erfolgreich getrennt wurde, false sonst.</returns>
        public async Task<bool> DisconnectFromPenAsync()
        {
            ThrowIfDisposed();

            if (_singlePenConnectionManager == null)
            {
                ThreadSafeConsole.WriteLine("Keine aktive Verbindung vorhanden.");
                return false;
            }

            try
            {
                await _singlePenConnectionManager.DisconnectAsync().ConfigureAwait(false);
                _singlePenConnectionManager.Dispose();
                _singlePenConnectionManager = null;
                ThreadSafeConsole.WriteLine("Verbindung erfolgreich getrennt.");

                // Event auslösen, dass Verbindung getrennt wurde
                OnConnectionStatusChanged(false);

                return true;
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Trennen der Verbindung: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Prüft, ob ein Stift mit entsprechender MAC-Adresse verbunden ist.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stiftes.</param>
        /// <returns>True, wenn der Stift verbunden ist, false sonst.</returns>
        public bool IsPenConnected(string macAddress)
        {
            if (_singlePenConnectionManager == null)
            {
                return false;
            }

            var connectionInfo = _singlePenConnectionManager.ConnectionInfo;

            if (connectionInfo.ConnectionState != PenConnectionState.Authenticated)
            {
                return false;
            }

            // Beide MAC-Adressen normalisieren für Vergleich
            string normalizedInputMac = MacAddressHelper.NormalizeMacAddress(macAddress);
            string normalizedConnectedMac = MacAddressHelper.NormalizeMacAddress(connectionInfo.MacAddress ?? "");

            // Prüfen nach MAC-Adresse (case-insensitive, format-unabhängig)
            if (!string.IsNullOrEmpty(normalizedConnectedMac) && normalizedConnectedMac.Equals(normalizedInputMac, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Prüfe, ob auf dem Host-Gerät Bluetooth aktiviert ist.
        /// Wrapped GetBluetoothIsEnabledAsync() aus der SDK-Komponente.
        /// </summary>
        /// <returns>True, wenn Bluetooth aktiviert ist, false sonst.</returns>
        public async Task<bool> IsBluetoothEnabledAsync()
        {
            ThrowIfDisposed();
            return await _bluetoothStatusMonitor.IsBluetoothEnabledAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Aktiviert oder deaktiviert Bluetooth auf dem Host-Gerät.
        /// </summary>
        /// <param name="enabled">True, um Bluetooth zu aktivieren, false um es zu deaktivieren.</param>
        /// <returns>True, wenn die Operation erfolgreich war, false sonst.</returns>
        public async Task<bool> SetBluetoothEnabledAsync(bool enabled)
        {
            ThrowIfDisposed();
            return await _bluetoothStatusMonitor.SetBluetoothEnabledAsync(enabled).ConfigureAwait(false);
        }

        /// <summary>
        /// Setzt den DisplayName (Bluetooth Local Name) des verbundenen Stifts.
        /// </summary>
        /// <param name="displayName">Der neue DisplayName (max. 16 Zeichen).</param>
        /// <returns>True, wenn erfolgreich, false sonst.</returns>
        public async Task<bool> SetDisplayNameAsync(string displayName)
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return false;
            }
            return await connectionManager.SetDisplayNameAsync(displayName).ConfigureAwait(false);
        }

        /// <summary>
        /// Fordert den aktuellen Batteriestatus des verbundenen Stifts an.
        /// Der Batteriestatus wird über das PenStatusReceived Event aktualisiert.
        /// </summary>
        public void RequestBatteryStatus()
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return;
            }
            connectionManager.RequestBatteryStatus();
        }

        /// <summary>
        /// Setzt das Passwort des verbundenen Stifts.
        /// </summary>
        /// <param name="oldPassword">Das aktuelle Passwort. Leeres String ("") wenn noch kein Passwort gesetzt wurde.</param>
        /// <param name="newPassword">Das neue Passwort (min. 4 Zeichen empfohlen, nicht "0000").</param>
        /// <returns>True, wenn erfolgreich, false sonst.</returns>
        public async Task<bool> SetPasswordAsync(string oldPassword, string newPassword)
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return false;
            }
            if (connectionManager.ConnectionInfo.ConnectionState != PenConnectionState.Authenticated)
            {
                ThreadSafeConsole.WriteLine("Stift ist nicht authentifiziert. Bitte warten Sie, bis die Verbindung vollständig hergestellt ist.");
                return false;
            }
            return await connectionManager.SetPasswordAsync(oldPassword, newPassword).ConfigureAwait(false);
        }

        /// <summary>
        /// Entfernt das Passwort des verbundenen Stifts.
        /// </summary>
        /// <param name="currentPassword">Das aktuelle Passwort.</param>
        /// <returns>True, wenn erfolgreich, false sonst.</returns>
        public async Task<bool> RemovePasswordAsync(string currentPassword)
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return false;
            }
            if (connectionManager.ConnectionInfo.ConnectionState != PenConnectionState.Authenticated)
            {
                ThreadSafeConsole.WriteLine("Stift ist nicht authentifiziert. Bitte warten Sie, bis die Verbindung vollständig hergestellt ist.");
                return false;
            }
            return await connectionManager.RemovePasswordAsync(currentPassword).ConfigureAwait(false);
        }

        /// <summary>
        /// Gibt das Passwort manuell ein, wenn der Stift danach fragt.
        /// </summary>
        /// <param name="password">Das einzugebende Passwort.</param>
        /// <returns>True, wenn das Passwort erfolgreich eingegeben wurde, false sonst.</returns>
        public bool InputPassword(string password)
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return false;
            }
            return connectionManager.InputPassword(password);
        }

        /// <summary>
        /// Prüft, ob aktuell ein Passwort für die Verbindung benötigt wird.
        /// </summary>
        /// <returns>True, wenn ein Passwort benötigt wird, false sonst.</returns>
        public bool IsPasswordRequired()
        {
            var connectionManager = GetConnectionManager();
            return connectionManager?.IsPasswordInputPending() ?? false;
        }

        void OnDotReceivedCallback(IPenClient sender, DotReceivedEventArgs args)
        {
            try
            {
                DotReceived?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Auslösen des DotReceived Events: {ex.Message}");
            }
        }

        /// <summary>
        /// Fordert die Liste der verfügbaren Offline-Daten vom verbundenen Stift an.
        /// </summary>
        public void RequestOfflineDataList()
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return;
            }
            connectionManager.RequestOfflineDataList();
        }

        /// <summary>
        /// Fordert den Download von Offline-Daten für eine bestimmte Note an.
        /// </summary>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="note">Note Id.</param>
        /// <param name="deleteOnFinished">True, wenn die Daten nach dem Download gelöscht werden sollen.</param>
        /// <param name="pages">Optional: Array von Seiten-Nummern, die heruntergeladen werden sollen.</param>
        /// <returns>True, wenn die Anfrage erfolgreich war, false sonst.</returns>
        public bool RequestOfflineData(int section, int owner, int note, bool deleteOnFinished = true, int[]? pages = null)
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return false;
            }
            return connectionManager.RequestOfflineData(section, owner, note, deleteOnFinished, pages);
        }

        /// <summary>
        /// Entfernt Offline-Daten vom verbundenen Stift.
        /// </summary>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="notes">Array von Note Ids, die entfernt werden sollen.</param>
        public void RequestRemoveOfflineData(int section, int owner, int[] notes)
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return;
            }
            connectionManager.RequestRemoveOfflineData(section, owner, notes);
        }

        /// <summary>
        /// Löscht alle Notizen des verbundenen Stifts.
        /// </summary>
        public void DeleteAllNotes()
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return;
            }
            connectionManager.DeleteAllNotes();
        }

        /// <summary>
        /// Konfiguriert verfügbare Notizen für den verbundenen Stift.
        /// Der Stift sendet nur Dots für konfigurierte Notizen.
        /// </summary>
        /// <param name="section">Section Id (optional).</param>
        /// <param name="owner">Owner Id (optional).</param>
        /// <param name="notes">Array von Note Ids (optional). Wenn null, werden alle Notizen aktiviert.</param>
        public void AddAvailableNote(int? section = null, int? owner = null, int[]? notes = null)
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return;
            }
            connectionManager.AddAvailableNote(section, owner, notes);
        }

        /// <summary>
        /// Lädt alle Strokes für eine bestimmte Seite des verbundenen Stifts.
        /// </summary>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="note">Note Id.</param>
        /// <param name="page">Page Number.</param>
        /// <returns>Liste der Strokes für die angegebene Seite.</returns>
        public async Task<List<Stroke>> GetStrokesAsync(int section, int owner, int note, int page)
        {
            ThrowIfDisposed();
            var connectionManager = GetConnectionManagerOrLog();
            if (connectionManager == null)
            {
                return new List<Stroke>();
            }
            return await connectionManager.GetStrokesAsync(section, owner, note, page).ConfigureAwait(false);
        }

        /// <summary>
        /// Berechnet die Page-Dimensionen (Breite und Höhe) für eine bestimmte Seite.
        /// </summary>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="note">Note Id.</param>
        /// <param name="page">Page Number.</param>
        /// <param name="dpi">DPI für die Pixel-Konvertierung. Standard: 96 DPI.</param>
        /// <returns>Page-Dimensionen in Pixeln und Millimetern.</returns>
        public async Task<PageDimensions> GetPageDimensionsAsync(int section, int owner, int note, int page, float dpi = 96.0f)
        {
            ThrowIfDisposed();
            var strokes = await GetStrokesAsync(section, owner, note, page).ConfigureAwait(false);
            return NCodeCoordinateConverter.CalculatePageDimensions(strokes, dpi);
        }

        /// <summary>
        /// Prüft threadsafe, ob das Objekt bereits freigegeben wurde.
        /// </summary>
        /// <returns>True, wenn das Objekt bereits freigegeben wurde, false sonst.</returns>
        private bool IsDisposed()
        {
            lock (_disposeLock)
            {
                return _disposed;
            }
        }

        /// <summary>
        /// Wirft eine ObjectDisposedException, wenn das Objekt bereits freigegeben wurde.
        /// Thread-safe.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (IsDisposed())
            {
                throw new ObjectDisposedException(nameof(TeiPenService));
            }
        }

        /// <summary>
        /// Implementiert die IDisposable-Schnittstelle und gibt alle Ressourcen frei.
        /// Thread-safe. Verwendet Double-Checked-Locking-Pattern.
        /// </summary>
        public void Dispose()
        {
            bool shouldDispose = false;
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    ThreadSafeConsole.WriteLine("TeiPenService ist bereits freigegeben.");
                    return;
                }
                _disposed = true;
                shouldDispose = true;
            }

            // Alle Cleanup-Operationen außerhalb des Locks ausführen
            // (verhindert Deadlocks und hält Lock-Zeit minimal)
            if (shouldDispose)
            {
                // Bluetooth-Status-Überwachung stoppen
                try
                {
                    _bluetoothStatusMonitor?.StopHostDeviceBluetoothMonitoring();
                    _bluetoothStatusMonitor?.Dispose();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine("Fehler beim Disposen des BluetoothStatusMonitor: " + ex.Message);
                }

                // Pen Discovery Service disposen
                try
                {
                    _penDiscoveryService?.Dispose();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine("Fehler beim Disposen des PenDiscoveryService: " + ex.Message);
                }

                // SinglePenConnectionManager disposen
                try
                {
                    _singlePenConnectionManager?.Dispose();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine("Fehler beim Disposen des SinglePenConnectionManager: " + ex.Message);
                }

                // Memory Service disposen
                try
                {
                    _memoryService?.Dispose();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine("Fehler beim Disposen des MemoryService: " + ex.Message);
                }

                // SDK-Komponenten disposen
                try
                {
                    _discoveryPenClient?.Dispose();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine("Fehler beim Disposen der SDK-Komponenten: " + ex.Message);
                }

                // Pens in Reichweite leeren
                try
                {
                    _pensInReach?.Clear();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine("Fehler beim Leeren der Pens in Reichweite: " + ex.Message);
                }

                // Out-of-Range-Timestamps leeren
                try
                {
                    _penOutOfRangeTimestamps?.Clear();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine("Fehler beim Leeren der Out-of-Range-Timestamps: " + ex.Message);
                }

                // Last Update Timestamps leeren
                try
                {
                    _penLastUpdateTimestamps?.Clear();
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine("Fehler beim Leeren der Last Update Timestamps: " + ex.Message);
                }
            }
        }
    }
}

