#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TeiPenServiceConnectionManager.Utilities;
using TeiPenServiceConnectionManager.Models;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Service für die persistente Speicherung von gekoppelten Stiften.
    /// Läuft "still im Hintergrund" und speichert automatisch Stifte, wenn sie das erste Mal verbunden werden.
    /// </summary>
    public class MemoryService : DisposableBase
    {
        private readonly SemaphoreSlim _fileSemaphore = new SemaphoreSlim(1, 1);
        private readonly object _memoryLock = new object();
        private TeiPenServiceMemoryModel? _memoryModel;

        /// <summary>
        /// Pfad zum memory-Ordner (relativ zum Projektordner/AppDomain.BaseDirectory).
        /// </summary>
        public string MemoryDirectoryPath { get; private set; } = string.Empty;

        /// <summary>
        /// Vollständiger Pfad zur memory.json Datei.
        /// </summary>
        public string MemoryFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Initialisiert den MemoryService und lädt vorhandene Daten.
        /// </summary>
        public async Task InitializeAsync()
        {
            ThrowIfDisposed();

            try
            {
                // Pfade setzen
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                MemoryDirectoryPath = Path.Combine(baseDirectory, "memory");
                MemoryFilePath = Path.Combine(MemoryDirectoryPath, "memory.json");

                // Memory-Ordner erstellen, falls nicht vorhanden
                if (!Directory.Exists(MemoryDirectoryPath))
                {
                    Directory.CreateDirectory(MemoryDirectoryPath);
                    ThreadSafeConsole.WriteLine($"Memory-Ordner erstellt: {MemoryDirectoryPath}");
                }

                // Memory-Datei laden oder leere Struktur erstellen
                await LoadMemoryAsync().ConfigureAwait(false);

                // Memory-Datei sofort erstellen, falls sie nicht existiert (auch wenn leer)
                if (!File.Exists(MemoryFilePath))
                {
                    await SaveMemoryAsync().ConfigureAwait(false);
                    ThreadSafeConsole.WriteLine($"Memory-Datei erstellt: {MemoryFilePath}");
                }
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler bei der Initialisierung des MemoryService: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Lädt die memory.json Datei oder erstellt eine leere Struktur, falls nicht vorhanden.
        /// </summary>
        private async Task LoadMemoryAsync()
        {
            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(MemoryFilePath))
                {
                    try
                    {
                        string jsonContent = await Task.Run(() => File.ReadAllText(MemoryFilePath)).ConfigureAwait(false);
                        _memoryModel = JsonConvert.DeserializeObject<TeiPenServiceMemoryModel>(jsonContent)
                            ?? new TeiPenServiceMemoryModel();
                    }
                    catch (JsonException ex)
                    {
                        ThreadSafeConsole.WriteLine($"Fehler beim Parsen der memory.json: {ex.Message}. Erstelle neue Datei.");
                        _memoryModel = new TeiPenServiceMemoryModel();
                    }
                }
                else
                {
                    _memoryModel = new TeiPenServiceMemoryModel();
                }

                // Sicherstellen, dass PairedPens initialisiert ist
                if (_memoryModel.PairedPens == null)
                {
                    _memoryModel.PairedPens = new Dictionary<string, PenMemoryEntry>();
                }
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Speichert die memory.json Datei thread-safe.
        /// </summary>
        private async Task SaveMemoryAsync()
        {
            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                TeiPenServiceMemoryModel modelToSave;
                lock (_memoryLock)
                {
                    if (_memoryModel == null)
                    {
                        throw new InvalidOperationException("MemoryModel wurde nicht initialisiert.");
                    }
                    modelToSave = _memoryModel;
                }

                // JSON-Serialisierung mit expliziter NullValueHandling-Einstellung
                // NullValueHandling.Include stellt sicher, dass null-Werte als "null" gespeichert werden
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include
                };
                string jsonContent = JsonConvert.SerializeObject(modelToSave, settings);
                await Task.Run(() => File.WriteAllText(MemoryFilePath, jsonContent)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Speichern der memory.json: {ex.Message}");
                throw;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// Fügt einen neuen Stift hinzu oder aktualisiert einen bestehenden.
        /// Prüft, ob der Stift bereits vorhanden ist und aktualisiert nur LastConnectedAt bei bestehenden Einträgen.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (wird normalisiert).</param>
        /// <param name="deviceId">Windows Device ID des Stifts.</param>
        /// <param name="penName">Name des Stifts (aus SDK: PenInformation.Name).</param>
        /// <param name="displayName">Anzeigename des Stifts (aus Windows.DeviceInformation: DisplayName).</param>
        /// <param name="protocol">Protokollversion des Stifts (aus SDK: PenInformation.Protocol).</param>
        public async Task AddOrUpdatePenAsync(string macAddress, string deviceId, string penName, string displayName, int protocol)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            try
            {
                // MAC-Adresse normalisieren
                string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);
                DateTime now = DateTime.UtcNow;

                lock (_memoryLock)
                {
                    if (_memoryModel == null)
                    {
                        throw new InvalidOperationException("MemoryModel wurde nicht initialisiert.");
                    }

                    if (_memoryModel.PairedPens.TryGetValue(normalizedMacAddress, out PenMemoryEntry? existingEntry))
                    {
                        // Stift bereits vorhanden - nur LastConnectedAt aktualisieren
                        existingEntry.LastConnectedAt = now;
                        // Optional: DeviceId, PenName, DisplayName, Protocol aktualisieren, falls sich geändert haben
                        if (!string.IsNullOrEmpty(deviceId))
                        {
                            existingEntry.DeviceId = deviceId;
                        }
                        if (!string.IsNullOrEmpty(penName))
                        {
                            existingEntry.PenName = penName;
                        }
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            existingEntry.DisplayName = displayName;
                        }
                        existingEntry.Protocol = protocol;
                    }
                    else
                    {
                        // Neuer Stift - vollständigen Eintrag erstellen
                        var newEntry = new PenMemoryEntry
                        {
                            MacAddress = normalizedMacAddress,
                            DeviceId = deviceId ?? string.Empty,
                            PenName = penName ?? string.Empty,
                            DisplayName = displayName ?? string.Empty,
                            Protocol = protocol,
                            FirstConnectedAt = now,
                            LastConnectedAt = now
                        };
                        _memoryModel.PairedPens[normalizedMacAddress] = newEntry;
                    }
                }

                // Speichern
                await SaveMemoryAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Hinzufügen/Aktualisieren des Stifts in memory.json: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Prüft, ob ein Stift bereits in der memory.json existiert.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (wird normalisiert).</param>
        /// <returns>True, wenn der Stift bekannt ist, false sonst.</returns>
        public async Task<bool> IsPenKnownAsync(string macAddress)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                return false;
            }

            // Sicherstellen, dass Memory geladen ist
            if (_memoryModel == null)
            {
                await LoadMemoryAsync().ConfigureAwait(false);
            }

            string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);

            lock (_memoryLock)
            {
                if (_memoryModel == null)
                {
                    return false;
                }
                return _memoryModel.PairedPens.ContainsKey(normalizedMacAddress);
            }
        }

        /// <summary>
        /// Gibt alle gekoppelten Stifte zurück.
        /// </summary>
        /// <returns>Dictionary mit allen gekoppelten Stiften (Key: normalisierte MAC-Adresse).</returns>
        public async Task<Dictionary<string, PenMemoryEntry>> GetPairedPensAsync()
        {
            ThrowIfDisposed();

            // Sicherstellen, dass Memory geladen ist
            if (_memoryModel == null)
            {
                await LoadMemoryAsync().ConfigureAwait(false);
            }

            lock (_memoryLock)
            {
                if (_memoryModel == null)
                {
                    return new Dictionary<string, PenMemoryEntry>();
                }
                // Kopie zurückgeben, um Thread-Safety zu gewährleisten
                return new Dictionary<string, PenMemoryEntry>(_memoryModel.PairedPens);
            }
        }

        /// <summary>
        /// Entfernt einen bestimmten gekoppelten Stift aus der memory.json.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (wird normalisiert).</param>
        /// <returns>True, wenn der Stift entfernt wurde, false wenn nicht gefunden.</returns>
        public async Task<bool> RemovePairedPenAsync(string macAddress)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            try
            {
                // MAC-Adresse normalisieren
                string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);

                // Sicherstellen, dass Memory geladen ist
                if (_memoryModel == null)
                {
                    await LoadMemoryAsync().ConfigureAwait(false);
                }

                bool removed = false;
                lock (_memoryLock)
                {
                    if (_memoryModel == null)
                    {
                        throw new InvalidOperationException("MemoryModel wurde nicht initialisiert.");
                    }

                    removed = _memoryModel.PairedPens.Remove(normalizedMacAddress);
                }

                if (removed)
                {
                    // Speichern
                    await SaveMemoryAsync().ConfigureAwait(false);
                    ThreadSafeConsole.WriteLine($"tei-Pen {normalizedMacAddress} wurde aus memory.json entfernt.");
                }
                else
                {
                    ThreadSafeConsole.WriteLine($"tei-Pen {normalizedMacAddress} wurde nicht in memory.json gefunden.");
                }

                return removed;
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Entfernen des gekoppelten Stifts: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ruft das gespeicherte Passwort eines gekoppelten Stifts aus der memory.json ab.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (wird normalisiert).</param>
        /// <returns>Das gespeicherte Passwort oder null, wenn kein Passwort gesetzt ist oder der Stift nicht gefunden wurde.</returns>
        public async Task<string?> GetPenPasswordAsync(string macAddress)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                return null;
            }

            try
            {
                // MAC-Adresse normalisieren
                string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);

                // Sicherstellen, dass Memory geladen ist
                if (_memoryModel == null)
                {
                    await LoadMemoryAsync().ConfigureAwait(false);
                }

                lock (_memoryLock)
                {
                    if (_memoryModel == null)
                    {
                        return null;
                    }

                    if (_memoryModel.PairedPens.TryGetValue(normalizedMacAddress, out PenMemoryEntry? entry) && entry != null)
                    {
                        return entry.Password;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Abrufen des Passworts: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Aktualisiert das Passwort eines gekoppelten Stifts in der memory.json.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (wird normalisiert).</param>
        /// <param name="password">Neues Passwort (null oder leer zum Entfernen, "0000" wird nicht gespeichert).</param>
        public async Task UpdatePenPasswordAsync(string macAddress, string? password)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            try
            {
                // MAC-Adresse normalisieren
                string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);

                // Sicherstellen, dass Memory geladen ist
                if (_memoryModel == null)
                {
                    await LoadMemoryAsync().ConfigureAwait(false);
                }

                lock (_memoryLock)
                {
                    if (_memoryModel == null)
                    {
                        throw new InvalidOperationException("MemoryModel wurde nicht initialisiert.");
                    }

                    if (_memoryModel.PairedPens.TryGetValue(normalizedMacAddress, out PenMemoryEntry? entry) && entry != null)
                    {
                        // Passwort nur speichern, wenn nicht null/leer und nicht "0000"
                        if (password != null && !string.IsNullOrEmpty(password) && !password.Equals("0000", StringComparison.Ordinal))
                        {
                            string? oldPassword = entry.Password;
                            entry.Password = password;
                            ThreadSafeConsole.WriteLine($"DEBUG: UpdatePenPasswordAsync - Passwort für Stift {normalizedMacAddress} von '{oldPassword ?? "null"}' auf '{password}' gesetzt");
                        }
                        else
                        {
                            // Passwort entfernen (null setzen)
                            string? oldPassword = entry.Password;
                            entry.Password = null;
                            ThreadSafeConsole.WriteLine($"DEBUG: UpdatePenPasswordAsync - Passwort für Stift {normalizedMacAddress} entfernt (war: '{oldPassword ?? "null"}')");
                        }
                    }
                    else
                    {
                        ThreadSafeConsole.WriteLine($"Warnung: Stift {normalizedMacAddress} nicht in memory.json gefunden. Passwort kann nicht aktualisiert werden.");
                        return;
                    }
                }

                // Speichern
                await SaveMemoryAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Aktualisieren des Passworts: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Entfernt alle gekoppelten Stifte aus der memory.json.
        /// </summary>
        public async Task RemoveAllPairedPensAsync()
        {
            ThrowIfDisposed();

            try
            {
                // Sicherstellen, dass Memory geladen ist
                if (_memoryModel == null)
                {
                    await LoadMemoryAsync().ConfigureAwait(false);
                }

                lock (_memoryLock)
                {
                    if (_memoryModel == null)
                    {
                        throw new InvalidOperationException("MemoryModel wurde nicht initialisiert.");
                    }

                    int count = _memoryModel.PairedPens.Count;
                    _memoryModel.PairedPens.Clear();
                    ThreadSafeConsole.WriteLine($"{count} gekoppelte(r)(n) tei-Pen(s) wurden aus memory.json entfernt.");
                }

                // Speichern
                await SaveMemoryAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Entfernen aller gekoppelten Stifte: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gibt Ressourcen frei.
        /// </summary>
        protected override void DisposeResources()
        {
            _fileSemaphore?.Dispose();
        }
    }
}
