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
    /// Service für die Passwort-Verwaltung eines Smartpens.
    /// Verwaltet SetPassword, RemovePassword, InputPassword und Event-Handler für Password-Events.
    /// </summary>
    public sealed partial class PenPasswordService : IDisposable
    {
        private readonly PenController _penControllerConcrete;
        private readonly PenConnectionInfoModel _connectionInfo;
        private readonly MemoryService? _memoryService;
        private readonly Func<bool> _hasActiveConnection;
        private readonly Func<bool> _isDisposed;
        private readonly Action<Action> _executeIfNotDisposed;
        private readonly Action _throwIfDisposed;
        private readonly Action<string>? _onPasswordRequiredCallback;
        private readonly Func<string, bool, Task>? _onPasswordChangedCallback;

        // TaskCompletionSource für PasswordChanged Event (thread-safe)
        private readonly object _passwordChangedLock = new object();
        private TaskCompletionSource<bool>? _passwordChangedTaskSource;
        private string? _pendingPassword;

        // TaskCompletionSource für manuelle Passwort-Eingabe (thread-safe)
        private readonly object _passwordInputLock = new object();
        private TaskCompletionSource<string>? _passwordInputTaskSource;

        // Flag für automatisches Passwort (wird verwendet, um Timeout zu erkennen)
        private bool _autoPasswordInProgress = false;

        // Temporäres Passwort, das nach erfolgreicher Authentifizierung in memory.json gespeichert wird
        private string? _pendingPasswordForMemory;

        /// <summary>
        /// Initialisiert den PenPasswordService.
        /// </summary>
        public PenPasswordService(
            PenController penControllerConcrete,
            PenConnectionInfoModel connectionInfo,
            Func<bool> hasActiveConnection,
            Func<bool> isDisposed,
            Action<Action> executeIfNotDisposed,
            Action throwIfDisposed,
            MemoryService? memoryService = null,
            Action<string>? onPasswordRequiredCallback = null,
            Func<string, bool, Task>? onPasswordChangedCallback = null)
        {
            _penControllerConcrete = penControllerConcrete ?? throw new ArgumentNullException(nameof(penControllerConcrete));
            _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
            _hasActiveConnection = hasActiveConnection ?? throw new ArgumentNullException(nameof(hasActiveConnection));
            _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
            _executeIfNotDisposed = executeIfNotDisposed ?? throw new ArgumentNullException(nameof(executeIfNotDisposed));
            _throwIfDisposed = throwIfDisposed ?? throw new ArgumentNullException(nameof(throwIfDisposed));
            _memoryService = memoryService;
            _onPasswordRequiredCallback = onPasswordRequiredCallback;
            _onPasswordChangedCallback = onPasswordChangedCallback;
        }

        /// <summary>
        /// Setzt das Passwort des verbundenen Stifts.
        /// </summary>
        /// <param name="oldPassword">Das aktuelle Passwort. Leeres String ("") wenn noch kein Passwort gesetzt wurde.</param>
        /// <param name="newPassword">Das neue Passwort (min. 4 Zeichen empfohlen, nicht "0000").</param>
        /// <returns>True, wenn erfolgreich, false sonst.</returns>
        public async Task<bool> SetPasswordAsync(string oldPassword, string newPassword)
        {
            _throwIfDisposed();

            // oldPassword kann leer sein, wenn noch kein Passwort gesetzt wurde (SDK verwendet intern "0000")
            // null ist nicht erlaubt
            if (oldPassword == null)
            {
                ThreadSafeConsole.WriteLine("Altes Passwort darf nicht null sein. Verwenden Sie \"\" (leeres String), wenn noch kein Passwort gesetzt wurde.");
                return false;
            }

            if (string.IsNullOrEmpty(newPassword))
            {
                ThreadSafeConsole.WriteLine("Neues Passwort darf nicht leer sein.");
                return false;
            }

            // Validierung: Nicht "0000" als neues Passwort (Default-Passwort)
            if (newPassword.Equals("0000", StringComparison.Ordinal))
            {
                ThreadSafeConsole.WriteLine("Das Default-Passwort '0000' kann nicht als neues Passwort verwendet werden.");
                return false;
            }

            // Wenn oldPassword leer ist, bedeutet das: Noch kein Passwort gesetzt
            // Das SDK setzt intern leere Strings auf "0000" um
            // Wenn oldPassword "0000" ist, bedeutet das: Passwort wurde bereits auf "0000" gesetzt (nicht erlaubt)
            if (oldPassword.Equals("0000", StringComparison.Ordinal))
            {
                ThreadSafeConsole.WriteLine("Das Default-Passwort '0000' kann nicht als altes Passwort verwendet werden. Verwenden Sie \"\" (leeres String), wenn noch kein Passwort gesetzt wurde.");
                return false;
            }

            // Validierung: Min. 4 Zeichen empfohlen
            if (newPassword.Length < 4)
            {
                ThreadSafeConsole.WriteLine("Warnung: Passwort sollte mindestens 4 Zeichen lang sein.");
            }

            if (!EnsureConnected())
            {
                return false;
            }

            try
            {
                // TaskCompletionSource für Event-Handling erstellen
                TaskCompletionSource<bool> taskSource = new TaskCompletionSource<bool>();
                lock (_passwordChangedLock)
                {
                    _passwordChangedTaskSource = taskSource;
                    _pendingPassword = newPassword; // Neues Passwort für das Event speichern
                }

                // SDK-Methode aufrufen
                bool requestSent = _penControllerConcrete.SetPassword(oldPassword, newPassword);
                if (!requestSent)
                {
                    lock (_passwordChangedLock)
                    {
                        _passwordChangedTaskSource = null;
                        _pendingPassword = null;
                    }
                    ThreadSafeConsole.WriteLine("Fehler beim Senden der Passwort-Änderungsanfrage.");
                    return false;
                }

                // Auf PasswordChanged Event warten (mit Timeout von 10 Sekunden)
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    cts.Token.Register(() =>
                    {
                        lock (_passwordChangedLock)
                        {
                            if (_passwordChangedTaskSource != null)
                            {
                                _passwordChangedTaskSource.TrySetResult(false);
                                _passwordChangedTaskSource = null;
                                _pendingPassword = null;
                            }
                        }
                    });

                    bool success = await taskSource.Task.ConfigureAwait(false);

                    if (success)
                    {
                        ThreadSafeConsole.WriteLine("Passwort erfolgreich geändert.");
                    }
                    else
                    {
                        ThreadSafeConsole.WriteLine("Passwort-Änderung fehlgeschlagen. Bitte überprüfen Sie das alte Passwort. Wenn noch kein Passwort gesetzt wurde, verwenden Sie \"\" (leeres String) als altes Passwort.");
                    }

                    return success;
                }
            }
            catch (Exception ex)
            {
                lock (_passwordChangedLock)
                {
                    _passwordChangedTaskSource = null;
                    _pendingPassword = null;
                }
                ThreadSafeConsole.WriteLine($"Fehler beim Setzen des Passworts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Entfernt das Passwort des verbundenen Stifts.
        /// </summary>
        /// <param name="currentPassword">Das aktuelle Passwort.</param>
        /// <returns>True, wenn erfolgreich, false sonst.</returns>
        public async Task<bool> RemovePasswordAsync(string currentPassword)
        {
            _throwIfDisposed();

            if (string.IsNullOrEmpty(currentPassword))
            {
                ThreadSafeConsole.WriteLine("Aktuelles Passwort darf nicht leer sein.");
                return false;
            }

            // Validierung: Nicht "0000" (Default-Passwort)
            if (currentPassword.Equals("0000", StringComparison.Ordinal))
            {
                ThreadSafeConsole.WriteLine("Das Default-Passwort '0000' kann nicht verwendet werden.");
                return false;
            }

            if (!EnsureConnected())
            {
                return false;
            }

            try
            {
                // TaskCompletionSource für Event-Handling erstellen
                TaskCompletionSource<bool> taskSource = new TaskCompletionSource<bool>();
                lock (_passwordChangedLock)
                {
                    _passwordChangedTaskSource = taskSource;
                    _pendingPassword = null; // null bedeutet Passwort entfernen
                }

                // SDK-Methode aufrufen (leeres neues Passwort = Passwort entfernen)
                bool requestSent = _penControllerConcrete.SetPassword(currentPassword, "");
                if (!requestSent)
                {
                    lock (_passwordChangedLock)
                    {
                        _passwordChangedTaskSource = null;
                        _pendingPassword = null;
                    }
                    ThreadSafeConsole.WriteLine("Fehler beim Senden der Passwort-Entfernungsanfrage.");
                    return false;
                }

                // Auf PasswordChanged Event warten (mit Timeout von 10 Sekunden)
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    cts.Token.Register(() =>
                    {
                        lock (_passwordChangedLock)
                        {
                            if (_passwordChangedTaskSource != null)
                            {
                                _passwordChangedTaskSource.TrySetResult(false);
                                _passwordChangedTaskSource = null;
                                _pendingPassword = null;
                            }
                        }
                    });

                    bool success = await taskSource.Task.ConfigureAwait(false);

                    if (success)
                    {
                        ThreadSafeConsole.WriteLine("Passwort erfolgreich entfernt.");
                    }
                    else
                    {
                        ThreadSafeConsole.WriteLine("Passwort-Entfernung fehlgeschlagen. Bitte überprüfen Sie das aktuelle Passwort.");
                    }

                    return success;
                }
            }
            catch (Exception ex)
            {
                lock (_passwordChangedLock)
                {
                    _passwordChangedTaskSource = null;
                    _pendingPassword = null;
                }
                ThreadSafeConsole.WriteLine($"Fehler beim Entfernen des Passworts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gibt das Passwort manuell ein, wenn der Stift danach fragt.
        /// </summary>
        /// <param name="password">Das einzugebende Passwort.</param>
        /// <returns>True, wenn das Passwort erfolgreich eingegeben wurde, false sonst.</returns>
        public bool InputPassword(string password)
        {
            _throwIfDisposed();

            if (string.IsNullOrEmpty(password))
            {
                ThreadSafeConsole.WriteLine("Passwort darf nicht leer sein.");
                return false;
            }

            lock (_passwordInputLock)
            {
                if (_passwordInputTaskSource != null)
                {
                    _passwordInputTaskSource.TrySetResult(password);
                    _passwordInputTaskSource = null;
                }
            }

            // Passwort an den Stift senden
            try
            {
                _penControllerConcrete.InputPassword(password);
                ThreadSafeConsole.WriteLine("Passwort wurde eingegeben. Warte auf Authentifizierung...");

                // WICHTIG: Passwort NICHT sofort in memory.json speichern!
                // Stattdessen temporär speichern und erst nach erfolgreicher Authentifizierung
                // (OnAuthenticationSucceeded) in memory.json schreiben.
                // Dies verhindert, dass falsche Passwörter gespeichert werden.
                lock (_passwordInputLock)
                {
                    _pendingPasswordForMemory = password;
                }

                return true;
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Eingeben des Passworts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Prüft, ob aktuell auf Passwort-Eingabe gewartet wird.
        /// </summary>
        /// <returns>True, wenn auf Passwort-Eingabe gewartet wird, false sonst.</returns>
        public bool IsPasswordInputPending()
        {
            lock (_passwordInputLock)
            {
                return _passwordInputTaskSource != null;
            }
        }

        /// <summary>
        /// Prüft, ob eine aktive Verbindung besteht. Schreibt eine Meldung und gibt false zurück, wenn nicht.
        /// </summary>
        /// <returns>True, wenn verbunden, sonst false.</returns>
        private bool EnsureConnected()
        {
            if (_hasActiveConnection())
            {
                return true;
            }
            ThreadSafeConsole.WriteLine("Kein Stift verbunden. Bitte zuerst eine Verbindung herstellen.");
            return false;
        }

        /// <summary>
        /// Implementiert das Dispose Pattern.
        /// </summary>
        public void Dispose()
        {
            lock (_passwordChangedLock)
            {
                _passwordChangedTaskSource?.TrySetCanceled();
                _passwordChangedTaskSource = null;
                _pendingPassword = null;
            }

            lock (_passwordInputLock)
            {
                _passwordInputTaskSource?.TrySetCanceled();
                _passwordInputTaskSource = null;
                _pendingPasswordForMemory = null;
            }
        }
    }
}
