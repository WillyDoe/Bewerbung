using System;
using System.Threading;
using System.Threading.Tasks;
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Utilities;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Service für die Kern-Verbindungslogik (Connect/Disconnect) eines Smartpens.
    /// Verwaltet Thread-Safety durch Semaphores und aktualisiert ConnectionInfo.
    /// </summary>
    public sealed class PenConnectionCoreService : IDisposable
    {
        private readonly IPenController _penController;
        private readonly GenericBluetoothPenClient _penClient;
        private readonly PenConnectionInfoModel _connectionInfo;

        // Semaphore für Disconnect-Operation (verhindert gleichzeitige Disconnect-Aufrufe)
        private readonly SemaphoreSlim _disconnectSemaphore = new SemaphoreSlim(1, 1);
        // Semaphore für Connect-Operation (verhindert gleichzeitige Connect-Aufrufe)
        private readonly SemaphoreSlim _connectSemaphore = new SemaphoreSlim(1, 1);

        // Thread-sichere dispose flag
        private readonly object _disposeLock = new object();
        private bool _disposed;

        // Thread-sicherer connection info lock
        private readonly object _connectionInfoLock = new object();

        // Delegates für Dispose-Checks (werden von übergeordneter Klasse bereitgestellt)
        private readonly Func<bool> _isDisposed;
        private readonly Action<Action> _executeIfNotDisposed;
        private readonly Action _throwIfDisposed;

        /// <summary>
        /// Initialisiert den PenConnectionCoreService.
        /// </summary>
        /// <param name="penController">PenController des SDKs.</param>
        /// <param name="penClient">GenericBluetoothPenClient für Verbindungen.</param>
        /// <param name="connectionInfo">Gemeinsam genutztes Verbindungsmodell.</param>
        /// <param name="isDisposed">Funktion, die prüft, ob das übergeordnete Objekt disposed wurde.</param>
        /// <param name="executeIfNotDisposed">Funktion, die eine Aktion ausführt, wenn nicht disposed.</param>
        /// <param name="throwIfDisposed">Funktion, die eine Exception wirft, wenn disposed.</param>
        public PenConnectionCoreService(
            IPenController penController,
            GenericBluetoothPenClient penClient,
            PenConnectionInfoModel connectionInfo,
            Func<bool> isDisposed,
            Action<Action> executeIfNotDisposed,
            Action throwIfDisposed)
        {
            _penController = penController ?? throw new ArgumentNullException(nameof(penController));
            _penClient = penClient ?? throw new ArgumentNullException(nameof(penClient));
            _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
            _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
            _executeIfNotDisposed = executeIfNotDisposed ?? throw new ArgumentNullException(nameof(executeIfNotDisposed));
            _throwIfDisposed = throwIfDisposed ?? throw new ArgumentNullException(nameof(throwIfDisposed));
        }

        /// <summary>
        /// Gibt an, ob aktuell eine aktive Verbindung verwaltet wird.
        /// </summary>
        public bool HasActiveConnection => _penClient.Alive;

        /// <summary>
        /// Verbindet mit einem Smartpen und aktualisiert die ConnectionInfo.
        /// Thread-safe: Verhindert gleichzeitige Connect-Aufrufe durch SemaphoreSlim.
        /// </summary>
        /// <param name="penInformation">Informationen über das zu verbindende Gerät.</param>
        /// <returns>True, wenn die Verbindung erfolgreich hergestellt wurde, false sonst.</returns>
        public async Task<bool> ConnectAsync(PenInformation penInformation)
        {
            // Erster Check vor await
            _throwIfDisposed();

            if (penInformation == null)
            {
                throw new ArgumentNullException(nameof(penInformation));
            }

            // Semaphore erwerben (verhindert gleichzeitige Connect-Aufrufe)
            await _connectSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                // Zweiter Check nach Semaphore-Akquisition (könnte zwischenzeitlich disposed worden sein)
                _throwIfDisposed();

                // Lokale Kopie für konsistenten Check (verhindert Race Conditions)
                bool alreadyAlive = _penClient.Alive;

                if (alreadyAlive)
                {
                    return true;
                }

                try
                {
                    // MAC-Adresse VOR Connect setzen, damit sie beim PasswordRequested Event verfügbar ist
                    // Dies ist wichtig, damit das gespeicherte Passwort automatisch geladen werden kann
                    string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(penInformation.MacAddress ?? "");
                    _executeIfNotDisposed(() =>
                    {
                        _connectionInfo.MacAddress = normalizedMacAddress;
                        _connectionInfo.DeviceId = penInformation.Id;
                        _connectionInfo.DisplayName = penInformation.Name;
                        _connectionInfo.VirtualMacAddress = penInformation.VirtualMacAddress;
                        _connectionInfo.Rssi = penInformation.Rssi;
                        _connectionInfo.PenName = penInformation.Name;
                        _connectionInfo.PenIsLe = penInformation.IsLe;
                        _connectionInfo.Protocol = _penController.Protocol;
                        // ConnectionState wird initial auf Disconnected gesetzt
                        // Wird später durch Connected/Authenticated Events aktualisiert
                        _connectionInfo.ConnectionState = PenConnectionState.Disconnected;
                    });

                    bool connected = await _penClient.Connect(penInformation).ConfigureAwait(false);

                    // Dispose-Check nach await (könnte zwischenzeitlich disposed worden sein)
                    _throwIfDisposed();

                    // Separate Lock für _connectionInfo Updates nach Connect
                    _executeIfNotDisposed(() =>
                    {
                        _connectionInfo.ClientAlive = _penClient.Alive;
                        _connectionInfo.ConnectedDeviceIsLe = _penClient.ConnectedDeviceIsLe;
                    });

                    // connected bedeutet nur, dass die physische Verbindung hergestellt wurde
                    // Die eigentliche Authentifizierung erfolgt erst im Authenticated Event
                    return connected && _penClient.Alive;
                }
                catch (ObjectDisposedException)
                {
                    // Objekt wurde disposed während des Connects - das ist ok
                    return false;
                }
                catch
                {
                    _executeIfNotDisposed(() =>
                    {
                        _connectionInfo.ClientAlive = false;
                        _connectionInfo.ConnectionState = PenConnectionState.Disconnected;
                    });

                    throw;
                }
            }
            finally
            {
                // Semaphore immer freigeben, auch bei Exceptions
                _connectSemaphore.Release();
            }
        }

        /// <summary>
        /// Trennt die Verbindung zu einem Smartpen und aktualisiert die ConnectionInfo.
        /// Thread-safe: Verhindert gleichzeitige Disconnect-Aufrufe durch SemaphoreSlim.
        /// </summary>
        public async Task DisconnectAsync()
        {
            // Erster Check vor await
            _throwIfDisposed();

            // Semaphore erwerben (verhindert gleichzeitige Disconnect-Aufrufe)
            await _disconnectSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                // Zweiter Check nach Semaphore-Akquisition (könnte zwischenzeitlich disposed worden sein)
                _throwIfDisposed();

                // Lokale Kopie für konsistente Checks (verhindert Race Conditions)
                bool wasAlive = _penClient.Alive;

                if (!wasAlive)
                {
                    // Separate Locks: Erst dispose-Check, dann connectionInfo-Update
                    _executeIfNotDisposed(() =>
                    {
                        _connectionInfo.ClientAlive = false;
                        _connectionInfo.ConnectionState = PenConnectionState.Disconnected;
                        _connectionInfo.WasAutoConnected = false; // Flag zurücksetzen
                    });

                    return;
                }

                try
                {
                    // Erster Disconnect-Versuch
                    await _penClient.Disconnect().ConfigureAwait(false);

                    // Dispose-Check nach await
                    _throwIfDisposed();

                    // Warte kurz, damit der Stift die Trennung verarbeiten kann
                    // Dies ist notwendig, damit die Statusleuchte des Stiftes aktualisiert wird
                    await Task.Delay(100).ConfigureAwait(false);

                    // Dispose-Check nach await
                    _throwIfDisposed();

                    // Prüfe, ob die Verbindung noch aktiv ist (kann bei LE-Verbindungen vorkommen)
                    // Lokale Kopie für konsistenten Check
                    bool stillAlive = _penClient.Alive;

                    if (stillAlive)
                    {
                        // Zweiter Disconnect-Versuch für LE-Verbindungen
                        // Das SDK hat einen Bug: BluetoothLePenClient.Disconnect() gibt die GATT-Ressourcen nicht frei
                        await Task.Delay(200).ConfigureAwait(false);

                        // Dispose-Check nach await
                        _throwIfDisposed();

                        // Prüfe erneut, ob die Verbindung noch aktiv ist
                        stillAlive = _penClient.Alive;

                        if (stillAlive)
                        {
                            // Versuche erneut zu trennen
                            try
                            {
                                await _penClient.Disconnect().ConfigureAwait(false);

                                // Dispose-Check nach await
                                _throwIfDisposed();

                                await Task.Delay(100).ConfigureAwait(false);

                                // Dispose-Check nach await
                                _throwIfDisposed();
                            }
                            catch (ObjectDisposedException)
                            {
                                // Objekt wurde disposed - das ist ok, einfach zurückkehren
                                return;
                            }
                            catch (Exception)
                            {
                                // Ignoriere Fehler beim zweiten Disconnect-Versuch
                                // (Die Verbindung könnte bereits getrennt sein)
                            }
                        }
                    }

                    // Separate Locks: Erst dispose-Check, dann connectionInfo-Update
                    _executeIfNotDisposed(() =>
                    {
                        _connectionInfo.ClientAlive = _penClient.Alive;
                        _connectionInfo.ConnectionState = PenConnectionState.Disconnected;
                        _connectionInfo.ConnectedDeviceIsLe = false;
                        _connectionInfo.WasAutoConnected = false; // Flag zurücksetzen
                    });
                }
                catch (ObjectDisposedException)
                {
                    // Objekt wurde disposed während des Disconnects - das ist ok
                    return;
                }
                catch
                {
                    // Separate Locks: Erst dispose-Check, dann connectionInfo-Update
                    _executeIfNotDisposed(() =>
                    {
                        _connectionInfo.ClientAlive = _penClient.Alive;
                        _connectionInfo.ConnectionState = _penClient.Alive ? PenConnectionState.Connected : PenConnectionState.Disconnected;
                        if (!_penClient.Alive)
                        {
                            _connectionInfo.WasAutoConnected = false; // Flag zurücksetzen, wenn getrennt
                        }
                    });

                    // Wirf Exception weiter, damit der Aufrufer informiert wird
                    throw;
                }
            }
            finally
            {
                // Semaphore immer freigeben, auch bei Exceptions
                _disconnectSemaphore.Release();
            }
        }

        /// <summary>
        /// Implementiert das Dispose Pattern und gibt Ressourcen frei.
        /// </summary>
        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }

            // Semaphores freigeben
            _connectSemaphore?.Dispose();
            _disconnectSemaphore?.Dispose();
        }
    }
}
