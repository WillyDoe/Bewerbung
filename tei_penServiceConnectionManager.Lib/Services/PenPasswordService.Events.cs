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
    /// Event-Handler-Teil des PenPasswordService (partielle Klasse).
    /// </summary>
    public sealed partial class PenPasswordService
    {
        /// <summary>
        /// Wird aufgerufen, wenn die Authentifizierung erfolgreich war.
        /// Setzt den automatischen Passwort-Flow zurück und speichert das Passwort in memory.json.
        /// </summary>
        public void OnAuthenticationSucceeded()
        {
            string? passwordToSave = null;
            string? macAddress = null;

            lock (_passwordInputLock)
            {
                _autoPasswordInProgress = false;
                if (_passwordInputTaskSource != null)
                {
                    _passwordInputTaskSource.TrySetCanceled();
                    _passwordInputTaskSource = null;
                }

                // Passwort für memory.json kopieren und zurücksetzen
                passwordToSave = _pendingPasswordForMemory;
                _pendingPasswordForMemory = null;
            }

            // MAC-Adresse außerhalb des Locks abrufen
            macAddress = _connectionInfo.MacAddress;

            // WICHTIG: Passwort nur nach erfolgreicher Authentifizierung in memory.json speichern
            if (_memoryService != null && !string.IsNullOrEmpty(macAddress) && !string.IsNullOrEmpty(passwordToSave))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        bool isKnown = await _memoryService.IsPenKnownAsync(macAddress!).ConfigureAwait(false);
                        if (!isKnown)
                        {
                            await _memoryService.AddOrUpdatePenAsync(
                                macAddress!,
                                _connectionInfo.DeviceId ?? string.Empty,
                                _connectionInfo.PenName ?? string.Empty,
                                _connectionInfo.DisplayName ?? string.Empty,
                                _connectionInfo.Protocol).ConfigureAwait(false);
                        }
                        await _memoryService.UpdatePenPasswordAsync(macAddress!, passwordToSave).ConfigureAwait(false);
                        ThreadSafeConsole.WriteLine($"DEBUG: Passwort für Stift {macAddress} nach erfolgreicher Authentifizierung in memory.json gespeichert.");
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Warnung: Passwort konnte nicht in memory.json gespeichert werden: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Handler für das PasswordRequested-Event des SDK.
        /// </summary>
        public void OnPasswordRequested(IPenClient sender, PasswordRequestedEventArgs args)
        {
            lock (_passwordInputLock)
            {
                if (_passwordInputTaskSource != null && _pendingPasswordForMemory != null)
                {
                    _pendingPasswordForMemory = null;
                }
                if (_passwordInputTaskSource != null)
                {
                    return;
                }
            }

            _executeIfNotDisposed(() =>
            {
                _connectionInfo.ConnectionState = PenConnectionState.PasswordRequired;
            });

            bool wasAutoConnected = _connectionInfo.WasAutoConnected;

            lock (_passwordInputLock)
            {
                if (_passwordInputTaskSource == null)
                {
                    _passwordInputTaskSource = new TaskCompletionSource<string>();
                }
            }

            if (_memoryService != null && !string.IsNullOrEmpty(_connectionInfo.MacAddress) && wasAutoConnected)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string? password = await _memoryService.GetPenPasswordAsync(_connectionInfo.MacAddress).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(password))
                        {
                            ThreadSafeConsole.WriteLine($"Passwort aus memory.json geladen. Authentifiziere automatisch...");
                            lock (_passwordInputLock)
                            {
                                _autoPasswordInProgress = true;
                            }
                            _penControllerConcrete.InputPassword(password);
                            await Task.Delay(5000).ConfigureAwait(false);

                            lock (_passwordInputLock)
                            {
                                if (_autoPasswordInProgress)
                                {
                                    bool isAuthenticated = false;
                                    _executeIfNotDisposed(() =>
                                    {
                                        isAuthenticated = _connectionInfo.ConnectionState == PenConnectionState.Authenticated;
                                    });
                                    if (!isAuthenticated)
                                    {
                                        _autoPasswordInProgress = false;
                                        _pendingPasswordForMemory = null;
                                        if (_passwordInputTaskSource != null)
                                        {
                                            _passwordInputTaskSource.TrySetCanceled();
                                            _passwordInputTaskSource = null;
                                        }
                                        ThreadSafeConsole.WriteLine("");
                                        ThreadSafeConsole.WriteLine("⚠️  Das gespeicherte Passwort ist nicht korrekt.");
                                        ThreadSafeConsole.WriteLine("Bitte geben Sie das Passwort manuell ein:");
                                        ThreadSafeConsole.WriteLine("");
                                        _passwordInputTaskSource = new TaskCompletionSource<string>();
                                        if (_onPasswordRequiredCallback != null && !string.IsNullOrEmpty(_connectionInfo.MacAddress))
                                        {
                                            try
                                            {
                                                _onPasswordRequiredCallback(_connectionInfo.MacAddress);
                                            }
                                            catch (Exception ex)
                                            {
                                                ThreadSafeConsole.WriteLine($"Fehler beim Ausführen des OnPasswordRequired-Callbacks: {ex.Message}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        _autoPasswordInProgress = false;
                                        if (_passwordInputTaskSource != null)
                                        {
                                            _passwordInputTaskSource.TrySetCanceled();
                                            _passwordInputTaskSource = null;
                                        }
                                    }
                                }
                            }
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Fehler beim Abrufen des Passworts aus memory.json: {ex.Message}");
                    }

                    ThreadSafeConsole.WriteLine("");
                    ThreadSafeConsole.WriteLine("⚠️  Der Stift benötigt ein Passwort für die Verbindung.");
                    if (wasAutoConnected)
                    {
                        ThreadSafeConsole.WriteLine("(Der Stift war bereits gekoppelt, hat aber mittlerweile ein Passwort erhalten.)");
                    }
                    ThreadSafeConsole.WriteLine("Bitte geben Sie das Passwort ein:");
                    ThreadSafeConsole.WriteLine("");
                    if (_onPasswordRequiredCallback != null && !string.IsNullOrEmpty(_connectionInfo.MacAddress))
                    {
                        try
                        {
                            _onPasswordRequiredCallback(_connectionInfo.MacAddress);
                        }
                        catch (Exception ex)
                        {
                            ThreadSafeConsole.WriteLine($"Fehler beim Ausführen des OnPasswordRequired-Callbacks: {ex.Message}");
                        }
                    }
                });
            }
            else
            {
                ThreadSafeConsole.WriteLine("");
                ThreadSafeConsole.WriteLine("⚠️  Der Stift benötigt ein Passwort für die Verbindung.");
                ThreadSafeConsole.WriteLine("Bitte geben Sie das Passwort ein:");
                ThreadSafeConsole.WriteLine("");
                if (_onPasswordRequiredCallback != null && !string.IsNullOrEmpty(_connectionInfo.MacAddress))
                {
                    try
                    {
                        _onPasswordRequiredCallback(_connectionInfo.MacAddress);
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Fehler beim Ausführen des OnPasswordRequired-Callbacks: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Fordert den Benutzer zur manuellen Passwort-Eingabe auf.
        /// </summary>
        private void RequestPasswordManually()
        {
            lock (_passwordInputLock)
            {
                if (_passwordInputTaskSource == null)
                {
                    ThreadSafeConsole.WriteLine("");
                    ThreadSafeConsole.WriteLine("⚠️  Der Stift benötigt ein Passwort für die Verbindung.");
                    ThreadSafeConsole.WriteLine("Bitte geben Sie das Passwort ein:");
                    ThreadSafeConsole.WriteLine("");
                    _passwordInputTaskSource = new TaskCompletionSource<string>();
                    if (_onPasswordRequiredCallback != null && !string.IsNullOrEmpty(_connectionInfo.MacAddress))
                    {
                        try
                        {
                            _onPasswordRequiredCallback(_connectionInfo.MacAddress);
                        }
                        catch (Exception ex)
                        {
                            ThreadSafeConsole.WriteLine($"Fehler beim Ausführen des OnPasswordRequired-Callbacks: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handler für das PasswordChanged-Event des SDK.
        /// </summary>
        public void OnPasswordChanged(IPenClient sender, SimpleResultEventArgs args)
        {
            if (args == null)
            {
                return;
            }

            lock (_passwordChangedLock)
            {
                if (_passwordChangedTaskSource != null)
                {
                    _passwordChangedTaskSource.TrySetResult(args.Result);
                    string? passwordToSave = _pendingPassword;
                    string? macAddress = _connectionInfo.MacAddress;
                    _passwordChangedTaskSource = null;
                    _pendingPassword = null;

                    if (_memoryService != null && !string.IsNullOrEmpty(macAddress) && args.Result)
                    {
                        string macAddressToUse = macAddress!;
                        if (passwordToSave == null)
                        {
                            ThreadSafeConsole.WriteLine($"DEBUG: OnPasswordChanged - Entferne Passwort für Stift {macAddressToUse} aus memory.json");
                        }
                        else
                        {
                            ThreadSafeConsole.WriteLine($"DEBUG: OnPasswordChanged - Speichere Passwort für Stift {macAddressToUse} in memory.json");
                        }
                        Task.Run(async () =>
                        {
                            try
                            {
                                ThreadSafeConsole.WriteLine($"DEBUG: Task.Run gestartet - passwordToSave={(passwordToSave == null ? "null" : $"'{passwordToSave}'")}");
                                bool isKnown = await _memoryService.IsPenKnownAsync(macAddressToUse).ConfigureAwait(false);
                                ThreadSafeConsole.WriteLine($"DEBUG: Stift {macAddressToUse} ist bekannt: {isKnown}");
                                if (!isKnown)
                                {
                                    ThreadSafeConsole.WriteLine($"DEBUG: Erstelle Stift {macAddressToUse} in memory.json");
                                    await _memoryService.AddOrUpdatePenAsync(
                                        macAddressToUse,
                                        _connectionInfo.DeviceId ?? string.Empty,
                                        _connectionInfo.PenName ?? string.Empty,
                                        _connectionInfo.DisplayName ?? string.Empty,
                                        _connectionInfo.Protocol).ConfigureAwait(false);
                                }
                                ThreadSafeConsole.WriteLine($"DEBUG: Rufe UpdatePenPasswordAsync auf mit passwordToSave={(passwordToSave == null ? "null" : $"'{passwordToSave}'")}");
                                await _memoryService.UpdatePenPasswordAsync(macAddressToUse, passwordToSave).ConfigureAwait(false);
                                ThreadSafeConsole.WriteLine($"DEBUG: UpdatePenPasswordAsync erfolgreich abgeschlossen");
                            }
                            catch (Exception ex)
                            {
                                ThreadSafeConsole.WriteLine($"Fehler beim Speichern des Passworts: {ex.Message}");
                                ThreadSafeConsole.WriteLine($"DEBUG: Exception beim Speichern: {ex}");
                            }
                        });
                    }
                    else
                    {
                        if (_memoryService == null)
                            ThreadSafeConsole.WriteLine($"DEBUG: OnPasswordChanged - _memoryService ist null, Passwort wird nicht gespeichert");
                        else if (string.IsNullOrEmpty(macAddress))
                            ThreadSafeConsole.WriteLine($"DEBUG: OnPasswordChanged - macAddress ist null/leer, Passwort wird nicht gespeichert");
                        else if (!args.Result)
                            ThreadSafeConsole.WriteLine($"DEBUG: OnPasswordChanged - args.Result ist false, Passwort wird nicht gespeichert");
                    }
                }
            }

            if (_onPasswordChangedCallback != null && !_isDisposed())
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _onPasswordChangedCallback(string.Empty, args.Result).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Fehler beim Ausführen des OnPasswordChanged-Callbacks: {ex.Message}");
                    }
                });
            }
        }
    }
}
