using System;
using System.Threading.Tasks;
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Utilities;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Event-Handler-Teil des SinglePenConnectionManager (partielle Klasse).
    /// </summary>
    public sealed partial class SinglePenConnectionManager
    {
        /// <summary>
        /// Handler für das Connected-Event des SDK.
        /// </summary>
        private void OnConnected(IPenClient sender, ConnectedEventArgs args)
        {
            if (args == null)
            {
                return;
            }

            ExecuteIfNotDisposed(() =>
            {
                // MAC-Adresse normalisieren (mit Doppelpunkten, großgeschrieben)
                _connectionInfo.MacAddress = MacAddressHelper.NormalizeMacAddress(args.MacAddress ?? "");
                _connectionInfo.DisplayName = args.DeviceName;
                // Physische Bluetooth-Verbindung hergestellt
                _connectionInfo.ClientAlive = true;
                _connectionInfo.ConnectionState = PenConnectionState.Connected;
            });

            // Connected Event bedeutet nur, dass die physische Bluetooth-Verbindung hergestellt wurde
            // Die Authentifizierung erfolgt erst im Authenticated Event (möglicherweise nach Passwort-Eingabe)
            // Callback wird erst aufgerufen, wenn Authenticated Event ausgelöst wird
        }

        /// <summary>
        /// Handler für das Disconnected-Event des SDK.
        /// </summary>
        private void OnDisconnected(IPenClient sender, object args)
        {
            ExecuteIfNotDisposed(() =>
            {
                _connectionInfo.ConnectionState = PenConnectionState.Disconnected;
                _connectionInfo.ClientAlive = false;
            });

            // Disconnected-Callback aufrufen (z.B. zum Auslösen von ConnectionStatusChanged)
            if (_onDisconnectedCallback != null && !IsDisposed())
            {
                try
                {
                    _onDisconnectedCallback();
                }
                catch (Exception ex)
                {
                    // Fehler beim Callback nicht kritisch - nur loggen
                    ThreadSafeConsole.WriteLine($"Fehler beim Ausführen des OnDisconnected-Callbacks: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handler für das Authenticated-Event des SDK.
        /// </summary>
        private void OnAuthenticated(IPenClient sender, object args)
        {
            ExecuteIfNotDisposed(() =>
            {
                // Jetzt ist die Verbindung vollständig authentifiziert
                _connectionInfo.ConnectionState = PenConnectionState.Authenticated;
            });

            // Passwort-Aufforderung zurücksetzen, da Authentifizierung erfolgreich war
            _passwordService.OnAuthenticationSucceeded();

            // Authenticated Event bedeutet, dass die Verbindung vollständig authentifiziert wurde
            // Jetzt kann der Stift verwendet werden

            // Callback aufrufen, falls vorhanden (z.B. zum Speichern im Memory mit korrektem DisplayName)
            // Wird hier aufgerufen, da die Verbindung jetzt vollständig authentifiziert ist
            if (_onConnectedCallback != null && !IsDisposed())
            {
                // Callback asynchron im Hintergrund ausführen (Fire-and-Forget)
                Task.Run(async () =>
                {
                    try
                    {
                        await _onConnectedCallback().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Fehler beim Callback nicht kritisch - nur loggen
                        ThreadSafeConsole.WriteLine($"Fehler beim Ausführen des OnConnected-Callbacks: {ex.Message}");
                    }
                });
            }

            // Console-Ausgabe VOR dem Callback, damit sie vor den Verbindungsoptionen angezeigt wird
            ThreadSafeConsole.WriteLine($"Stift {_connectionInfo.MacAddress} erfolgreich authentifiziert und verbunden.");

            // Authenticated-Callback aufrufen (z.B. zum Auslösen von ConnectionStatusChanged)
            if (_onAuthenticatedCallback != null && !IsDisposed())
            {
                try
                {
                    _onAuthenticatedCallback();
                }
                catch (Exception ex)
                {
                    // Fehler beim Callback nicht kritisch - nur loggen
                    ThreadSafeConsole.WriteLine($"Fehler beim Ausführen des OnAuthenticated-Callbacks: {ex.Message}");
                }
            }

            // WICHTIG: Verfügbare Notizen konfigurieren, damit der Stift Dots sendet
            // Ohne diese Konfiguration sendet der Stift keine Dots!
            // Mit leerem Aufruf werden alle Notizen aktiviert
            try
            {
                _penControllerConcrete.AddAvailableNote();
                ThreadSafeConsole.WriteLine("Verfügbare Notizen konfiguriert - Stift kann jetzt Dots senden.");
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Warnung: Konnte verfügbare Notizen nicht konfigurieren: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler für das DotReceived-Event des SDK.
        /// </summary>
        private void OnDotReceived(IPenClient sender, DotReceivedEventArgs args)
        {
            if (args == null || args.Dot == null)
            {
                ThreadSafeConsole.WriteLine("DEBUG: OnDotReceived - args oder Dot ist null");
                return;
            }

            // Prüfen, ob Verbindung authentifiziert ist
            if (_connectionInfo.ConnectionState != PenConnectionState.Authenticated)
            {
                ThreadSafeConsole.WriteLine($"DEBUG: OnDotReceived - Verbindung nicht authentifiziert. Status: {_connectionInfo.ConnectionState}");
                return;
            }

            // Echtzeit (UI, OCR, Konsole): Callback immer, sobald authentifiziert — unabhängig von MAC.
            // Zuvor wurde bei fehlender MAC vor dem Callback abgebrochen; dann kamen keine Stift-Dots in der App an.
            if (_onDotReceivedCallback != null && !IsDisposed())
            {
                try
                {
                    _onDotReceivedCallback.Invoke(sender, args);
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Ausführen des OnDotReceived-Callbacks: {ex.Message}");
                }
            }

            string macAddress = _connectionInfo.MacAddress ?? "";
            if (string.IsNullOrEmpty(macAddress))
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    string? displayName = _connectionInfo.DisplayName;
                    await _dataStorageService.SaveDotAsync(macAddress, args.Dot, displayName, args).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Speichern des Dots: {ex.Message}");
                    ThreadSafeConsole.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            });
        }

        /// <summary>
        /// Handler für das PenFound-Event des SDK.
        /// </summary>
        private void OnPenFound(IPenClient sender, PenInformation args)
        {
            if (args == null)
            {
                return;
            }

            ExecuteIfNotDisposed(() =>
            {
                _connectionInfo.DeviceId = args.Id;
                _connectionInfo.DisplayName = args.Name;
                // MAC-Adresse normalisieren (mit Doppelpunkten, großgeschrieben)
                _connectionInfo.MacAddress = MacAddressHelper.NormalizeMacAddress(args.MacAddress ?? "");
                _connectionInfo.VirtualMacAddress = args.VirtualMacAddress;
                _connectionInfo.Rssi = args.Rssi;
                _connectionInfo.PenName = args.Name;
                _connectionInfo.PenIsLe = args.IsLe;
                _connectionInfo.Protocol = args.Protocol;
            });
        }

        /// <summary>
        /// Handler für das PenUpdated-Event des SDK.
        /// </summary>
        private void OnPenUpdated(IPenClient sender, PenUpdateInformation args)
        {
            if (args == null)
            {
                return;
            }

            ExecuteIfNotDisposed(() =>
            {
                _connectionInfo.ModelName = args.ModelName;
                _connectionInfo.UpdateRssi = args.Rssi;
            });
        }

        /// <summary>
        /// Handler für das SearchStopped-Event des SDK.
        /// </summary>
        private void OnSearchStopped(IPenClient sender, Windows.Devices.Bluetooth.BluetoothError args)
        {
            // Currently no-op; hook available for future error handling.
        }

        /// <summary>
        /// Handler für das PenStatusReceived-Event des SDK.
        /// Wird von PenEventSubscriptionService aufgerufen.
        /// </summary>
        private void OnPenStatusReceived(IPenClient sender, PenStatusReceivedEventArgs args)
        {
            if (args == null)
            {
                return;
            }

            ExecuteIfNotDisposed(() =>
            {
                _connectionInfo.BatteryStatus = args.Battery;
            });
        }

        /// <summary>
        /// Handler für das OfflineDataListReceived-Event des SDK.
        /// </summary>
        private void OnOfflineDataListReceived(IPenClient sender, OfflineDataListReceivedEventArgs args)
        {
            if (args == null || args.OfflineNotes == null)
            {
                return;
            }

            ThreadSafeConsole.WriteLine($"Offline-Daten-Liste empfangen: {args.OfflineNotes.Length} Note(s) verfügbar.");

            // Prüfen, ob alle Notizen gelöscht werden sollen
            bool shouldDeleteAll = false;
            lock (_deleteAllNotesLock)
            {
                if (_deleteAllNotesPending)
                {
                    shouldDeleteAll = true;
                    _deleteAllNotesPending = false; // Flag zurücksetzen
                }
            }

            if (shouldDeleteAll)
            {
                DeleteAllNotesFromList(args.OfflineNotes);

                // Auch lokale Dateien löschen
                if (!string.IsNullOrEmpty(_connectionInfo.MacAddress))
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            int deletedFiles = await _dataStorageService.DeleteAllLocalFilesAsync(_connectionInfo.MacAddress).ConfigureAwait(false);
                            ThreadSafeConsole.WriteLine($"Lokale Dateien gelöscht: {deletedFiles} Datei(en).");
                        }
                        catch (Exception ex)
                        {
                            ThreadSafeConsole.WriteLine($"Fehler beim Löschen der lokalen Dateien: {ex.Message}");
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Löscht alle Notizen aus der übergebenen Liste.
        /// </summary>
        private void DeleteAllNotesFromList(Neosmartpen.Net.OfflineDataInfo[] offlineNotes)
        {
            if (offlineNotes == null || offlineNotes.Length == 0)
            {
                ThreadSafeConsole.WriteLine("Keine Notizen zum Löschen gefunden.");
                return;
            }

            ThreadSafeConsole.WriteLine($"Lösche {offlineNotes.Length} Note(s)...");

            int deletedCount = 0;
            int failedCount = 0;

            foreach (var noteInfo in offlineNotes)
            {
                try
                {
                    // Gruppiere nach Section/Owner für effizientes Löschen
                    // RequestRemoveOfflineData löscht alle Notes eines Section/Owner-Paares
                    _dataTransferService.RequestRemoveOfflineData(noteInfo.Section, noteInfo.Owner, new[] { noteInfo.Note });
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Löschen der Note (Section: {noteInfo.Section}, Owner: {noteInfo.Owner}, Note: {noteInfo.Note}): {ex.Message}");
                    failedCount++;
                }
            }

            ThreadSafeConsole.WriteLine($"Löschvorgang abgeschlossen: {deletedCount} Note(s) gelöscht, {failedCount} Fehler.");
        }

        /// <summary>
        /// Handler für das OfflineStrokeReceived-Event des SDK.
        /// </summary>
        private void OnOfflineStrokeReceived(IPenClient sender, OfflineStrokeReceivedEventArgs args)
        {
            if (args == null || args.Strokes == null || args.Strokes.Length == 0)
            {
                return;
            }

            // Offline-Strokes an StorageService weiterleiten für Persistierung
            if (!string.IsNullOrEmpty(_connectionInfo.MacAddress))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        string? displayName = _connectionInfo.DisplayName;
                        await _dataStorageService.SaveOfflineStrokesAsync(_connectionInfo.MacAddress, args.Strokes, displayName).ConfigureAwait(false);
                        ThreadSafeConsole.WriteLine($"Offline-Strokes gespeichert: {args.AmountDone}/{args.Total}");
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Fehler beim Speichern der Offline-Strokes: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Handler für das OfflineDownloadFinished-Event des SDK.
        /// </summary>
        private void OnOfflineDownloadFinished(IPenClient sender, SimpleResultEventArgs args)
        {
            if (args == null)
            {
                return;
            }

            if (args.Result)
            {
                ThreadSafeConsole.WriteLine("Offline-Daten-Download erfolgreich abgeschlossen.");
            }
            else
            {
                ThreadSafeConsole.WriteLine("Offline-Daten-Download fehlgeschlagen.");
            }
        }

        /// <summary>
        /// Handler für das OfflineDataDownloadStarted-Event des SDK.
        /// </summary>
        private void OnOfflineDataDownloadStarted(IPenClient sender, object args)
        {
            ThreadSafeConsole.WriteLine("Offline-Daten-Download gestartet.");
        }
    }
}
