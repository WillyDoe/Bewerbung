using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TeiPenServiceConnectionManager.Models;
using tei_penService_ui.Models;

namespace tei_penService_ui.Services
{
    /// <summary>
    /// Zentraler Service für alle App-Daten (Users + PairedPens).
    /// Speichert in app_data/app_data.json, synchronisiert PairedPens aus der Lib.
    /// </summary>
    public class AppDataService
    {
        /// <summary>Serieller Zugriff auf Datei-Operationen (Lesen/Schreiben app_data.json).</summary>
        private readonly SemaphoreSlim _fileSemaphore = new SemaphoreSlim(1, 1);
        /// <summary>Lock für Zugriff auf <see cref="_appData"/> (In-Memory-Modell).</summary>
        private readonly object _dataLock = new object();
        /// <summary>In-Memory-Datenmodell (Users, PairedPens).</summary>
        private AppDataModel _appData;

        /// <summary>Pfad zum App-Daten-Verzeichnis (app_data).</summary>
        private string AppDataDirectoryPath { get; set; } = string.Empty;
        /// <summary>Vollständiger Pfad zu app_data.json.</summary>
        private string AppDataFilePath { get; set; } = string.Empty;
        /// <summary>Vollständiger Pfad zur Lib memory.json.</summary>
        private string LibMemoryFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Initialisiert den Service und lädt vorhandene Daten.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                AppDataDirectoryPath = Path.Combine(baseDirectory, "app_data");
                AppDataFilePath = Path.Combine(AppDataDirectoryPath, "app_data.json");
                LibMemoryFilePath = Path.Combine(baseDirectory, "memory", "memory.json");

                if (!Directory.Exists(AppDataDirectoryPath))
                {
                    Directory.CreateDirectory(AppDataDirectoryPath);
                }

                await LoadAppDataAsync();

                if (!File.Exists(AppDataFilePath))
                {
                    await SaveAppDataAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler bei der Initialisierung des AppDataService: {ex.Message}");
                throw;
            }
        }

        /// <summary>Lädt app_data.json in <see cref="_appData"/> und stellt Users/PairedPens-Listen bereit.</summary>
        private async Task LoadAppDataAsync()
        {
            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(AppDataFilePath))
                {
                    try
                    {
                        string jsonContent = await Task.Run(() => File.ReadAllText(AppDataFilePath)).ConfigureAwait(false);
                        _appData = JsonConvert.DeserializeObject<AppDataModel>(jsonContent) ?? new AppDataModel();
                    }
                    catch (JsonException ex)
                    {
                        Debug.WriteLine($"Fehler beim Parsen der app_data.json: {ex.Message}. Erstelle neue Datei.");
                        _appData = new AppDataModel();
                    }
                }
                else
                {
                    _appData = new AppDataModel();
                }

                if (_appData.Users == null)
                    _appData.Users = new Dictionary<string, UserMemoryEntry>();
                if (_appData.PairedPens == null)
                    _appData.PairedPens = new Dictionary<string, PenMemoryEntry>();
                else
                    MigratePairedPensKeysToCanonical();
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Speichert die Daten in die Datei. Darf nur aufgerufen werden, wenn der Aufrufer bereits _fileSemaphore hält (vermeidet Deadlock).
        /// </summary>
        private async Task SaveAppDataCoreAsync()
        {
            AppDataModel modelToSave;
            lock (_dataLock)
            {
                modelToSave = _appData;
            }
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };
            string jsonContent = JsonConvert.SerializeObject(modelToSave, settings);
            await Task.Run(() => File.WriteAllText(AppDataFilePath, jsonContent)).ConfigureAwait(false);
        }

        /// <summary>Speichert <see cref="_appData"/> in app_data.json (thread-sicher mit Semaphore).</summary>
        private async Task SaveAppDataAsync()
        {
            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await SaveAppDataCoreAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Speichern der app_data.json: {ex.Message}");
                throw;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Synchronisiert PairedPens aus der Lib memory.json ins AppData.
        /// </summary>
        public async Task SyncPairedPensFromLibAsync()
        {
            try
            {
                if (!File.Exists(LibMemoryFilePath))
                    return;

                string jsonContent = await Task.Run(() => File.ReadAllText(LibMemoryFilePath)).ConfigureAwait(false);
                var libModel = JsonConvert.DeserializeObject<TeiPenServiceMemoryModel>(jsonContent);

                if (libModel?.PairedPens == null)
                    return;

                await _fileSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    lock (_dataLock)
                    {
                        foreach (var kv in libModel.PairedPens)
                        {
                            string mac = NormalizeMacAddressCanonical(kv.Key);
                            if (string.IsNullOrEmpty(mac))
                                continue;
                            var libEntry = kv.Value;
                            if (libEntry == null)
                                continue;
                            // App-Eintrag finden (per kanonischer MAC), damit PenName/Password aus app_data erhalten bleiben
                            PenMemoryEntry appEntry = null;
                            if (_appData.PairedPens.TryGetValue(mac, out var direct))
                                appEntry = direct;
                            else
                            {
                                foreach (var appKv in _appData.PairedPens)
                                {
                                    if (string.Equals(NormalizeMacAddressCanonical(appKv.Key), mac, StringComparison.Ordinal))
                                    {
                                        appEntry = appKv.Value;
                                        break;
                                    }
                                }
                            }
                            if (appEntry != null)
                            {
                                var merged = new PenMemoryEntry
                                {
                                    MacAddress = libEntry.MacAddress,
                                    DeviceId = libEntry.DeviceId,
                                    PenName = !string.IsNullOrEmpty(appEntry.PenName) ? appEntry.PenName : libEntry.PenName,
                                    DisplayName = libEntry.DisplayName,
                                    Protocol = libEntry.Protocol,
                                    FirstConnectedAt = libEntry.FirstConnectedAt,
                                    LastConnectedAt = libEntry.LastConnectedAt,
                                    Password = appEntry.Password ?? libEntry.Password
                                };
                                _appData.PairedPens[mac] = merged;
                            }
                            else
                            {
                                _appData.PairedPens[mac] = libEntry;
                            }
                        }
                    }
                }
                finally
                {
                    _fileSemaphore.Release();
                }

                await SaveAppDataAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Synchronisieren der PairedPens: {ex.Message}");
            }
        }

        /// <summary>Normalisiert MAC für interne Vergleiche: Trim und Großbuchstaben.</summary>
        private static string NormalizeMacAddress(string mac)
        {
            return string.IsNullOrWhiteSpace(mac) ? string.Empty : mac.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Kanonische MAC für einheitliche Lookups: nur Hex-Zeichen 0-9A-F (alle Trennzeichen entfernt).
        /// Ermöglicht Zuordnung unabhängig vom Format (z. B. 9C:7B:D2:1A:19:E6, 9C-7B-D2, 9C.7B.D2).
        /// </summary>
        public static string NormalizeMacAddressCanonical(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac))
                return string.Empty;
            var sb = new System.Text.StringBuilder(12);
            foreach (char c in mac.Trim().ToUpperInvariant())
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Migriert PairedPens-Keys auf kanonische MAC (nur Hex-Zeichen) für einheitliche Lookups.</summary>
        private void MigratePairedPensKeysToCanonical()
        {
            if (_appData?.PairedPens == null)
                return;
            var migrated = new Dictionary<string, PenMemoryEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _appData.PairedPens)
            {
                string canonical = NormalizeMacAddressCanonical(kv.Key);
                if (!string.IsNullOrEmpty(canonical))
                    migrated[canonical] = kv.Value;
            }
            _appData.PairedPens = migrated;
        }

        /// <summary>Normalisiert E-Mail für Dictionary-Keys: Trim und Kleinbuchstaben.</summary>
        private static string NormalizeEmail(string email)
        {
            return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
        }

        #region User-Methoden

        /// <summary>
        /// Erstellt einen neuen Benutzer.
        /// </summary>
        public async Task<UserMemoryEntry> CreateUserAsync(string email, string displayName, string password)
        {
            string normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrEmpty(normalizedEmail))
                return null;

            UserMemoryEntry createdUser = null;
            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (_dataLock)
                {
                    if (_appData.Users.ContainsKey(normalizedEmail))
                        return null;

                    var user = new UserMemoryEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        Email = email.Trim(),
                        DisplayName = displayName?.Trim() ?? string.Empty,
                        Password = password ?? string.Empty,
                        CreatedAt = DateTime.UtcNow
                    };
                    _appData.Users[normalizedEmail] = user;
                    createdUser = user;
                }
            }
            finally
            {
                _fileSemaphore.Release();
            }

            await SaveAppDataAsync().ConfigureAwait(false);
            return createdUser;
        }

        /// <summary>
        /// Gibt den Benutzer anhand der Id zurück.
        /// </summary>
        public async Task<UserMemoryEntry> GetUserByIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var user in _appData.Users.Values)
                {
                    if (string.Equals(user?.Id, userId, StringComparison.Ordinal))
                        return user;
                }
                return null;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Gibt den Benutzer anhand der E-Mail zurück.
        /// </summary>
        public async Task<UserMemoryEntry> GetUserByEmailAsync(string email)
        {
            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                string normalizedEmail = NormalizeEmail(email);
                if (_appData.Users.TryGetValue(normalizedEmail, out var user))
                    return user;
                return null;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Gibt den Benutzer anhand der E-Mail oder des Anzeigenamens (Anmeldename) zurück.
        /// Zuerst wird per E-Mail gesucht, danach per DisplayName (Groß-/Kleinschreibung ignoriert).
        /// </summary>
        public async Task<UserMemoryEntry> GetUserByEmailOrDisplayNameAsync(string emailOrDisplayName)
        {
            if (string.IsNullOrWhiteSpace(emailOrDisplayName))
                return null;

            var user = await GetUserByEmailAsync(emailOrDisplayName).ConfigureAwait(false);
            if (user != null)
                return user;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                string search = emailOrDisplayName.Trim();
                foreach (var u in _appData.Users.Values)
                {
                    if (string.Equals(u.DisplayName?.Trim(), search, StringComparison.OrdinalIgnoreCase))
                        return u;
                }
                return null;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Aktualisiert die E-Mail-Adresse eines Benutzers (Users-Dictionary wird neu keyed).
        /// </summary>
        public async Task<bool> UpdateUserEmailAsync(string userId, string newEmail)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(newEmail))
                return false;

            string normalizedNew = NormalizeEmail(newEmail);
            if (string.IsNullOrEmpty(normalizedNew))
                return false;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                UserMemoryEntry user = null;
                string oldKey = null;
                foreach (var kv in _appData.Users)
                {
                    if (kv.Value?.Id == userId)
                    {
                        user = kv.Value;
                        oldKey = kv.Key;
                        break;
                    }
                }
                if (user == null || oldKey == null)
                    return false;
                if (oldKey == normalizedNew)
                    return true;

                lock (_dataLock)
                {
                    _appData.Users.Remove(oldKey);
                    if (_appData.Users.ContainsKey(normalizedNew))
                    {
                        _appData.Users[oldKey] = user;
                        return false;
                    }
                    user.Email = newEmail.Trim();
                    _appData.Users[normalizedNew] = user;
                }
                await SaveAppDataCoreAsync().ConfigureAwait(false);
                return true;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Aktualisiert das Passwort eines Benutzers.
        /// </summary>
        public async Task<bool> UpdateUserPasswordAsync(string userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var user in _appData.Users.Values)
                {
                    if (user?.Id == userId)
                    {
                        user.Password = newPassword ?? string.Empty;
                        await SaveAppDataCoreAsync().ConfigureAwait(false);
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Aktualisiert den Anzeigenamen eines Benutzers.
        /// </summary>
        public async Task<bool> UpdateUserDisplayNameAsync(string userId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var user in _appData.Users.Values)
                {
                    if (user?.Id == userId)
                    {
                        user.DisplayName = (displayName ?? string.Empty).Trim();
                        await SaveAppDataCoreAsync().ConfigureAwait(false);
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Validiert Anmeldedaten und gibt den Benutzer bei Erfolg zurück.
        /// loginIdentifier kann E-Mail oder Anmeldename (DisplayName) sein.
        /// </summary>
        public async Task<UserMemoryEntry> ValidateLoginAsync(string email, string password)
        {
            var user = await GetUserByEmailOrDisplayNameAsync(email).ConfigureAwait(false);
            if (user == null)
                return null;
            if (user.Password != (password ?? string.Empty))
                return null;
            return user;
        }

        /// <summary>
        /// Gibt den Benutzer zurück, der mit dem angegebenen Stift verknüpft ist.
        /// </summary>
        public async Task<UserMemoryEntry> GetUserByLinkedPenAsync(string macAddress)
        {
            string normalizedMac = NormalizeMacAddressCanonical(macAddress);
            if (string.IsNullOrEmpty(normalizedMac))
                return null;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var user in _appData.Users.Values)
                {
                    if (string.Equals(NormalizeMacAddressCanonical(user.LinkedPenMacAddress), normalizedMac, StringComparison.Ordinal))
                        return user;
                }
                return null;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Verknüpft einen Stift mit einem Benutzer.
        /// </summary>
        public async Task<bool> LinkPenToUserAsync(string macAddress, string userId)
        {
            string normalizedMac = NormalizeMacAddressCanonical(macAddress);
            if (string.IsNullOrEmpty(normalizedMac))
                return false;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var user in _appData.Users.Values)
                {
                    if (string.Equals(NormalizeMacAddressCanonical(user.LinkedPenMacAddress), normalizedMac, StringComparison.Ordinal))
                        user.LinkedPenMacAddress = null;
                }

                foreach (var user in _appData.Users.Values)
                {
                    if (user.Id == userId)
                    {
                        user.LinkedPenMacAddress = normalizedMac;
                        await SaveAppDataCoreAsync().ConfigureAwait(false);
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Entfernt die Verknüpfung eines Stifts von einem Benutzer.
        /// </summary>
        public async Task<bool> UnlinkPenFromUserAsync(string macAddress)
        {
            string normalizedMac = NormalizeMacAddressCanonical(macAddress);
            if (string.IsNullOrEmpty(normalizedMac))
                return false;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var user in _appData.Users.Values)
                {
                    if (string.Equals(NormalizeMacAddressCanonical(user.LinkedPenMacAddress), normalizedMac, StringComparison.Ordinal))
                    {
                        user.LinkedPenMacAddress = null;
                        await SaveAppDataCoreAsync().ConfigureAwait(false);
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        #endregion

        #region PairedPens-Methoden

        /// <summary>
        /// Gibt eine Kopie der gekoppelten Stifte zurück (Key = MAC, Value = PenMemoryEntry).
        /// </summary>
        public async Task<IReadOnlyDictionary<string, PenMemoryEntry>> GetPairedPensAsync()
        {
            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var copy = new Dictionary<string, PenMemoryEntry>(_appData.PairedPens ?? new Dictionary<string, PenMemoryEntry>(), StringComparer.OrdinalIgnoreCase);
                return copy;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Aktualisiert den Anzeigenamen (DisplayName) eines gekoppelten Stifts in app_data.
        /// </summary>
        public async Task<bool> UpdatePenDisplayNameAsync(string macAddress, string displayName)
        {
            string normalizedMac = NormalizeMacAddressCanonical(macAddress);
            if (string.IsNullOrEmpty(normalizedMac))
                return false;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_appData.PairedPens.TryGetValue(normalizedMac, out var entry))
                {
                    entry.DisplayName = (displayName ?? string.Empty).Trim();
                    await SaveAppDataCoreAsync().ConfigureAwait(false);
                    return true;
                }
                return false;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Aktualisiert den Stiftnamen (PenName) eines gekoppelten Stifts in app_data.
        /// </summary>
        public async Task<bool> UpdatePenNameAsync(string macAddress, string penName)
        {
            string normalizedMac = NormalizeMacAddressCanonical(macAddress);
            if (string.IsNullOrEmpty(normalizedMac))
                return false;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_appData.PairedPens.TryGetValue(normalizedMac, out var entry))
                {
                    entry.PenName = (penName ?? string.Empty).Trim();
                    await SaveAppDataCoreAsync().ConfigureAwait(false);
                    return true;
                }
                return false;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Speichert das Stift-Passwort in app_data (nur lokale Persistenz; Gerät ggf. separat über Lib setzen).
        /// </summary>
        public async Task<bool> SetPenPasswordInAppDataAsync(string macAddress, string password)
        {
            string normalizedMac = NormalizeMacAddressCanonical(macAddress);
            if (string.IsNullOrEmpty(normalizedMac))
                return false;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_appData.PairedPens.TryGetValue(normalizedMac, out var entry))
                {
                    entry.Password = string.IsNullOrWhiteSpace(password) ? null : password.Trim();
                    await SaveAppDataCoreAsync().ConfigureAwait(false);
                    return true;
                }
                return false;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        #endregion
    }
}
