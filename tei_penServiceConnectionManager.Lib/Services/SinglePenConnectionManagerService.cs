using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Utilities;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Verwaltet die Bluetooth-Verbindung zu genau einem Smartpen.
    /// Orchestriert spezialisierte Services für Connection, Password, Configuration und Events.
    /// </summary>
    public sealed partial class SinglePenConnectionManager : IDisposable
    {
        private readonly IPenController _penController;
        private readonly GenericBluetoothPenClient _penClient;
        private readonly PenController _penControllerConcrete;
        private readonly PenConnectionInfoModel _connectionInfo;
        private readonly MemoryService? _memoryService;

        // Spezialisierte Services
        private readonly PenConnectionCoreService _connectionCoreService;
        private readonly PenPasswordService _passwordService;
        private readonly PenEventSubscriptionService _eventSubscriptionService;
        private readonly PenDataTransferService _dataTransferService;
        private readonly PenDataStorageService _dataStorageService;

        // Thread-sichere dispose flag
        private readonly object _disposeLock = new object();
        private bool _disposed;

        // Flag für das Löschen aller Notizen
        private bool _deleteAllNotesPending = false;
        private readonly object _deleteAllNotesLock = new object();

        // Optionaler Callback, der aufgerufen wird, wenn das Connected-Event gefeuert wurde
        private readonly Func<Task>? _onConnectedCallback;
        // Optionaler Callback, der aufgerufen wird, wenn das Authenticated-Event gefeuert wurde
        private readonly Action? _onAuthenticatedCallback;
        // Optionaler Callback, der aufgerufen wird, wenn das Disconnected-Event gefeuert wurde (z.B. Stift ausgeschaltet)
        private readonly Action? _onDisconnectedCallback;

        // Optionaler Callback, der aufgerufen wird, wenn ein Dot empfangen wurde
        private readonly Action<IPenClient, DotReceivedEventArgs>? _onDotReceivedCallback;

        /// <summary>
        /// Initialisiert den Verbindungsmanager mit den core SDK-Komponenten.
        /// </summary>
        /// <param name="penController">PenController des SDKs (liefert Events & Protokollinformationen).</param>
        /// <param name="penClient">GenericBluetoothPenClient, der Discovery & Verbindungen kapselt.</param>
        /// <param name="connectionInfo">Gemeinsam genutztes Verbindungsmodell.</param>
        /// <param name="onConnectedCallback">Optionaler Callback, der aufgerufen wird, wenn das Connected-Event gefeuert wurde (z.B. zum Speichern im Memory).</param>
        /// <param name="onPasswordChangedCallback">Optionaler Callback, der aufgerufen wird, wenn das PasswordChanged-Event gefeuert wurde (Parameter: newPassword, success).</param>
        /// <param name="memoryService">Optionaler MemoryService für persistente Speicherung von Konfigurationsdaten.</param>
        /// <param name="onAuthenticatedCallback">Optionaler Callback, der aufgerufen wird, wenn das Authenticated-Event gefeuert wurde (z.B. zum Auslösen von ConnectionStatusChanged).</param>
        /// <param name="onDisconnectedCallback">Optionaler Callback, der aufgerufen wird, wenn das Disconnected-Event gefeuert wurde (z.B. Stift ausgeschaltet, Verbindung verloren).</param>
        /// <param name="onPasswordRequiredCallback">Optionaler Callback, der aufgerufen wird, wenn ein Passwort für die Verbindung benötigt wird (Parameter: macAddress).</param>
        /// <param name="dataStorageService">Optionaler PenDataStorageService für Datenpersistierung.</param>
        /// <param name="onDotReceivedCallback">Optionaler Callback, der aufgerufen wird, wenn ein Dot empfangen wurde (Parameter: penClient, dotArgs). Für Echtzeit-Anzeige in der UI.</param>
        public SinglePenConnectionManager(
            IPenController penController,
            GenericBluetoothPenClient penClient,
            PenConnectionInfoModel connectionInfo,
            Func<Task>? onConnectedCallback = null,
            Func<string, bool, Task>? onPasswordChangedCallback = null,
            MemoryService? memoryService = null,
            Action? onAuthenticatedCallback = null,
            Action? onDisconnectedCallback = null,
            Action<string>? onPasswordRequiredCallback = null,
            PenDataStorageService? dataStorageService = null,
            Action<IPenClient, DotReceivedEventArgs>? onDotReceivedCallback = null)
        {
            _penController = penController ?? throw new ArgumentNullException(nameof(penController));
            _penClient = penClient ?? throw new ArgumentNullException(nameof(penClient));
            _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
            _penControllerConcrete = penController as PenController ?? throw new ArgumentException("PenController instance is required for event subscriptions.", nameof(penController));
            _memoryService = memoryService;
            _onConnectedCallback = onConnectedCallback;
            _onAuthenticatedCallback = onAuthenticatedCallback;
            _onDisconnectedCallback = onDisconnectedCallback;
            _onDotReceivedCallback = onDotReceivedCallback;

            // Dispose-Check Delegates erstellen
            Func<bool> isDisposed = IsDisposed;
            Action<Action> executeIfNotDisposed = ExecuteIfNotDisposed;
            Action throwIfDisposed = ThrowIfDisposed;
            Func<bool> hasActiveConnection = () => _penClient.Alive;

            // Data Storage Service erstellen oder verwenden
            _dataStorageService = dataStorageService ?? new PenDataStorageService();
            Task.Run(async () =>
            {
                try
                {
                    await _dataStorageService.InitializeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler bei der Initialisierung des PenDataStorageService: {ex.Message}");
                }
            });

            // Connection Core Service erstellen
            _connectionCoreService = new PenConnectionCoreService(
                _penController,
                _penClient,
                _connectionInfo,
                isDisposed,
                executeIfNotDisposed,
                throwIfDisposed);

            // Password Service erstellen
            _passwordService = new PenPasswordService(
                _penControllerConcrete,
                _connectionInfo,
                hasActiveConnection,
                isDisposed,
                executeIfNotDisposed,
                throwIfDisposed,
                memoryService,
                onPasswordRequiredCallback,
                onPasswordChangedCallback);

            // Data Transfer Service erstellen
            _dataTransferService = new PenDataTransferService(
                _penControllerConcrete,
                isDisposed,
                executeIfNotDisposed,
                OnDotReceived,
                OnOfflineDataListReceived,
                OnOfflineStrokeReceived,
                OnOfflineDownloadFinished,
                OnOfflineDataDownloadStarted);

            // Event Subscription Service erstellen
            _eventSubscriptionService = new PenEventSubscriptionService(
                _penClient,
                _penControllerConcrete,
                OnConnected,
                OnDisconnected,
                _passwordService.OnPasswordRequested,
                _passwordService.OnPasswordChanged,
                OnAuthenticated,
                OnDotReceived,
                OnPenStatusReceived,
                OnPenFound,
                OnPenUpdated,
                OnSearchStopped,
                OnOfflineDataListReceived,
                OnOfflineStrokeReceived,
                OnOfflineDownloadFinished,
                OnOfflineDataDownloadStarted);
        }

        /// <summary>
        /// Gibt an, ob aktuell eine aktive Verbindung verwaltet wird.
        /// </summary>
        public bool HasActiveConnection => _connectionCoreService.HasActiveConnection;

        /// <summary>
        /// Gemeinsam genutztes Modell mit allen Verbindungsinformationen.
        /// </summary>
        public PenConnectionInfoModel ConnectionInfo => _connectionInfo;

        /// <summary>
        /// Verbindet mit einem Smartpen und aktualisiert die ConnectionInfo.
        /// </summary>
        /// <param name="penInformation">Informationen über das zu verbindende Gerät.</param>
        /// <returns>True, wenn die Verbindung erfolgreich hergestellt wurde, false sonst.</returns>
        public async Task<bool> ConnectAsync(PenInformation penInformation)
        {
            ThrowIfDisposed();
            return await _connectionCoreService.ConnectAsync(penInformation).ConfigureAwait(false);
        }

        /// <summary>
        /// Trennt die Verbindung zu einem Smartpen und aktualisiert die ConnectionInfo.
        /// </summary>
        public async Task DisconnectAsync()
        {
            ThrowIfDisposed();
            await _connectionCoreService.DisconnectAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Gibt die Verbindungsinformationen des verbundenen Smartpens zurück.
        /// </summary>
        /// <returns>Verbindungsinformationen des verbundenen Smartpens oder null, wenn kein Smartpen verbunden ist.</returns>
        public PenConnectionInfoModel? GetConnectedPenInfo()
        {
            if (_connectionInfo.ConnectionState != PenConnectionState.Authenticated)
            {
                return null;
            }
            return _connectionInfo;
        }

        /// <summary>
        /// Setzt den DisplayName (Bluetooth Local Name) des verbundenen Stifts.
        /// </summary>
        /// <param name="displayName">Der neue DisplayName (max. 16 Zeichen).</param>
        /// <returns>True, wenn erfolgreich, false sonst.</returns>
        public async Task<bool> SetDisplayNameAsync(string displayName)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(displayName))
            {
                ThreadSafeConsole.WriteLine("DisplayName darf nicht leer sein.");
                return false;
            }

            // Trim Whitespace
            displayName = displayName.Trim();

            // Validierung: Max. 16 Zeichen (Bluetooth Local Name Limit)
            if (displayName.Length > 16)
            {
                ThreadSafeConsole.WriteLine($"DisplayName darf maximal 16 Zeichen lang sein. Aktuell: {displayName.Length} Zeichen.");
                return false;
            }

            if (!EnsureConnected())
            {
                return false;
            }

            try
            {
                // SDK-Methode aufrufen
                _penControllerConcrete.SetBtLocalName(displayName);

                // ConnectionInfo aktualisieren
                ExecuteIfNotDisposed(() =>
                {
                    _connectionInfo.DisplayName = displayName;
                });

                // In MemoryService speichern, falls vorhanden
                if (_memoryService != null && !string.IsNullOrEmpty(_connectionInfo.MacAddress))
                {
                    try
                    {
                        await _memoryService.AddOrUpdatePenAsync(
                            _connectionInfo.MacAddress,
                            _connectionInfo.DeviceId,
                            _connectionInfo.PenName,
                            displayName,
                            _connectionInfo.Protocol).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Warnung: DisplayName konnte nicht in memory.json gespeichert werden: {ex.Message}");
                    }
                }

                ThreadSafeConsole.WriteLine($"DisplayName erfolgreich auf '{displayName}' gesetzt.");
                return true;
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Setzen des DisplayName: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fordert den aktuellen Batteriestatus des verbundenen Stifts an.
        /// Der Batteriestatus wird über das PenStatusReceived Event aktualisiert.
        /// </summary>
        public void RequestBatteryStatus()
        {
            ThrowIfDisposed();

            if (!EnsureConnected())
            {
                return;
            }

            try
            {
                // SDK-Methode aufrufen, um den Pen-Status anzufordern (enthält Batteriestatus)
                _penControllerConcrete.RequestPenStatus();
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Anfordern des Batteriestatus: {ex.Message}");
            }
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
            return await _passwordService.SetPasswordAsync(oldPassword, newPassword).ConfigureAwait(false);
        }

        /// <summary>
        /// Entfernt das Passwort des verbundenen Stifts.
        /// </summary>
        /// <param name="currentPassword">Das aktuelle Passwort.</param>
        /// <returns>True, wenn erfolgreich, false sonst.</returns>
        public async Task<bool> RemovePasswordAsync(string currentPassword)
        {
            ThrowIfDisposed();
            return await _passwordService.RemovePasswordAsync(currentPassword).ConfigureAwait(false);
        }

        /// <summary>
        /// Gibt das Passwort manuell ein, wenn der Stift danach fragt.
        /// </summary>
        /// <param name="password">Das einzugebende Passwort.</param>
        /// <returns>True, wenn das Passwort erfolgreich eingegeben wurde, false sonst.</returns>
        public bool InputPassword(string password)
        {
            ThrowIfDisposed();
            return _passwordService.InputPassword(password);
        }

        /// <summary>
        /// Prüft, ob aktuell auf Passwort-Eingabe gewartet wird.
        /// </summary>
        /// <returns>True, wenn auf Passwort-Eingabe gewartet wird, false sonst.</returns>
        public bool IsPasswordInputPending()
        {
            return _passwordService.IsPasswordInputPending();
        }

        /// <summary>
        /// Prüft threadsafe, ob das Objekt bereits freigegeben wurde.
        /// </summary>
        private bool IsDisposed()
        {
            lock (_disposeLock)
            {
                return _disposed;
            }
        }

        /// <summary>
        /// Prüft disposed flag und führt eine Aktion aus, wenn das Objekt nicht freigegeben wurde.
        /// Thread-safe. Verhindert Race Conditions zwischen Dispose() und Event-Handlern.
        /// </summary>
        private void ExecuteIfNotDisposed(Action action)
        {
            if (IsDisposed())
            {
                return;
            }

            lock (_connectionInfo)
            {
                // Zweiter Check (könnte zwischenzeitlich disposed worden sein.)
                if (IsDisposed())
                {
                    return;
                }

                action();
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
                throw new ObjectDisposedException(nameof(SinglePenConnectionManager));
            }
        }

        /// <summary>
        /// Prüft, ob eine aktive Verbindung besteht. Schreibt eine Meldung und gibt false zurück, wenn nicht.
        /// Nur nach ThrowIfDisposed() aufrufen.
        /// </summary>
        /// <returns>True, wenn verbunden, sonst false.</returns>
        private bool EnsureConnected()
        {
            if (HasActiveConnection)
            {
                return true;
            }
            ThreadSafeConsole.WriteLine("Kein Stift verbunden. Bitte zuerst eine Verbindung herstellen.");
            return false;
        }

        /// <summary>
        /// Fordert die Liste der verfügbaren Offline-Daten vom Stift an.
        /// </summary>
        public void RequestOfflineDataList()
        {
            ThrowIfDisposed();

            if (!EnsureConnected())
            {
                return;
            }

            try
            {
                _dataTransferService.RequestOfflineDataList();
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Anfordern der Offline-Daten-Liste: {ex.Message}");
            }
        }

        /// <summary>
        /// Löscht alle Notizen des verbundenen Stifts.
        /// Fordert zuerst die Offline-Daten-Liste an und löscht dann alle gefundenen Notizen.
        /// </summary>
        public void DeleteAllNotes()
        {
            ThrowIfDisposed();

            if (!EnsureConnected())
            {
                return;
            }

            lock (_deleteAllNotesLock)
            {
                _deleteAllNotesPending = true;
            }

            ThreadSafeConsole.WriteLine("Fordere Offline-Daten-Liste an, um alle Notizen zu löschen...");
            
            try
            {
                _dataTransferService.RequestOfflineDataList();
            }
            catch (Exception ex)
            {
                lock (_deleteAllNotesLock)
                {
                    _deleteAllNotesPending = false; // Flag zurücksetzen bei Fehler
                }
                ThreadSafeConsole.WriteLine($"Fehler beim Anfordern der Offline-Daten-Liste: {ex.Message}");
            }
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

            if (!EnsureConnected())
            {
                return false;
            }

            try
            {
                return _dataTransferService.RequestOfflineData(section, owner, note, deleteOnFinished, pages);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Anfordern der Offline-Daten: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Entfernt Offline-Daten vom Stift.
        /// </summary>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="notes">Array von Note Ids, die entfernt werden sollen.</param>
        public void RequestRemoveOfflineData(int section, int owner, int[] notes)
        {
            ThrowIfDisposed();

            if (!EnsureConnected())
            {
                return;
            }

            try
            {
                _dataTransferService.RequestRemoveOfflineData(section, owner, notes);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Entfernen der Offline-Daten: {ex.Message}");
            }
        }

        /// <summary>
        /// Konfiguriert verfügbare Notizen für den Stift.
        /// Der Stift sendet nur Dots für konfigurierte Notizen.
        /// WICHTIG: Ohne diese Konfiguration sendet der Stift keine Dots!
        /// </summary>
        /// <param name="section">Section Id (optional).</param>
        /// <param name="owner">Owner Id (optional).</param>
        /// <param name="notes">Array von Note Ids (optional). Wenn null, werden alle Notizen aktiviert.</param>
        public void AddAvailableNote(int? section = null, int? owner = null, int[]? notes = null)
        {
            ThrowIfDisposed();

            if (!EnsureConnected())
            {
                return;
            }

            try
            {
                if (section.HasValue && owner.HasValue)
                {
                    if (notes != null && notes.Length > 0)
                    {
                        _penControllerConcrete.AddAvailableNote(section.Value, owner.Value, notes);
                        ThreadSafeConsole.WriteLine($"Verfügbare Notizen konfiguriert: Section={section.Value}, Owner={owner.Value}, Notes={string.Join(",", notes)}");
                    }
                    else
                    {
                        _penControllerConcrete.AddAvailableNote(section.Value, owner.Value);
                        ThreadSafeConsole.WriteLine($"Verfügbare Notizen konfiguriert: Section={section.Value}, Owner={owner.Value} (alle Notes)");
                    }
                }
                else
                {
                    // Alle Notizen aktivieren
                    _penControllerConcrete.AddAvailableNote();
                    ThreadSafeConsole.WriteLine("Alle verfügbaren Notizen aktiviert - Stift kann jetzt Dots für alle Notizen senden.");
                }
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Konfigurieren der verfügbaren Notizen: {ex.Message}");
            }
        }

        /// <summary>
        /// Lädt alle Strokes für eine bestimmte Seite.
        /// </summary>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="note">Note Id.</param>
        /// <param name="page">Page Number.</param>
        /// <returns>Liste der Strokes für die angegebene Seite.</returns>
        public async Task<List<Stroke>> GetStrokesAsync(int section, int owner, int note, int page)
        {
            ThrowIfDisposed();

            if (!EnsureConnected())
            {
                return new List<Stroke>();
            }

            try
            {
                return await _dataStorageService.GetStrokesAsync(_connectionInfo.MacAddress, section, owner, note, page).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Laden der Strokes: {ex.Message}");
                return new List<Stroke>();
            }
        }

        /// <summary>
        /// Implementiert das Dispose Pattern und gibt alle Ressourcen frei.
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
                // Services in umgekehrter Reihenfolge disposen
                _eventSubscriptionService?.Dispose();
                _dataTransferService?.Dispose();
                _dataStorageService?.Dispose();
                _passwordService?.Dispose();
                _connectionCoreService?.Dispose();
            }
        }
    }
}
