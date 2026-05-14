#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Neosmartpen.Net;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Utilities;

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Service für die persistente Speicherung von Pen-Daten (Dots, Strokes).
    /// Speichert Real-time Dots als Strokes und persistiert Offline-Strokes.
    /// </summary>
    public sealed class PenDataStorageService : DisposableBase
    {
        private readonly SemaphoreSlim _fileSemaphore = new SemaphoreSlim(1, 1);
        private readonly object _dotBufferLock = new object();

        /// <summary>
        /// Pfad zum data-Ordner (relativ zum Projektordner/AppDomain.BaseDirectory).
        /// </summary>
        public string DataDirectoryPath { get; private set; } = string.Empty;

        /// <summary>
        /// In-Memory Buffer für Real-time Dots, die zu Strokes zusammengefasst werden.
        /// Key: "{macAddress}_{section}_{owner}_{note}_{page}", Value: Aktueller Stroke
        /// </summary>
        private readonly Dictionary<string, Stroke> _dotBuffers = new Dictionary<string, Stroke>();

        /// <summary>
        /// Initialisiert den PenDataStorageService und erstellt den Datenordner.
        /// </summary>
        public async Task InitializeAsync()
        {
            ThrowIfDisposed();

            try
            {
                // Pfade setzen
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                DataDirectoryPath = Path.Combine(baseDirectory, "data");

                // Data-Ordner erstellen, falls nicht vorhanden
                if (!Directory.Exists(DataDirectoryPath))
                {
                    Directory.CreateDirectory(DataDirectoryPath);
                    ThreadSafeConsole.WriteLine($"Data-Ordner erstellt: {DataDirectoryPath}");
                }
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler bei der Initialisierung des PenDataStorageService: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Speichert einen Dot. Sammelt Dots zu Strokes zusammen und speichert diese, wenn der Stroke vollständig ist (PEN_UP).
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (normalisiert).</param>
        /// <param name="dot">Der zu speichernde Dot.</param>
        /// <param name="displayName">Optional: DisplayName des Stifts.</param>
        /// <param name="dotReceivedEventArgs">Optional: wird bei PEN_UP nach erfolgreichem Stroke-Speichern an die Konsole geschrieben.</param>
        public async Task SaveDotAsync(string macAddress, Dot dot, string? displayName = null, DotReceivedEventArgs? dotReceivedEventArgs = null)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            if (dot == null)
            {
                throw new ArgumentNullException(nameof(dot));
            }

            try
            {
                string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);
                ThreadSafeConsole.WriteLine(
                    $"Dot — MAC={normalizedMacAddress}, Section={dot.Section}, Owner={dot.Owner}, Note={dot.Note}, Page={dot.Page}, Type={dot.DotType}, X={dot.X}, Y={dot.Y}");
                string bufferKey = GetBufferKey(normalizedMacAddress, dot.Section, dot.Owner, dot.Note, dot.Page);

                Stroke? currentStroke = null;

                lock (_dotBufferLock)
                {
                    if (!_dotBuffers.TryGetValue(bufferKey, out currentStroke))
                    {
                        // Neuer Stroke starten
                        currentStroke = new Stroke(dot.Section, dot.Owner, dot.Note, dot.Page);
                        _dotBuffers[bufferKey] = currentStroke;
                    }

                    // Dot zum Stroke hinzufügen
                    currentStroke.Add(dot);

                    // Wenn PEN_UP, Stroke speichern und aus Buffer entfernen
                    if (dot.DotType == DotTypes.PEN_UP)
                    {
                        _dotBuffers.Remove(bufferKey);
                    }
                }

                // Stroke speichern, wenn vollständig (PEN_UP)
                if (dot.DotType == DotTypes.PEN_UP && currentStroke != null)
                    await SaveStrokeAsync(normalizedMacAddress, currentStroke, displayName, dotReceivedEventArgs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Speichern des Dots: {ex.Message}");
            }
        }

        /// <summary>
        /// Speichert einen kompletten Stroke.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (normalisiert).</param>
        /// <param name="stroke">Der zu speichernde Stroke.</param>
        /// <param name="displayName">Optional: DisplayName des Stifts.</param>
        /// <param name="dotReceivedWhenSaved">Optional: wird nach erfolgreichem Speichern per <see cref="ThreadSafeConsole"/> ausgegeben.</param>
        public async Task SaveStrokeAsync(string macAddress, Stroke stroke, string? displayName = null, DotReceivedEventArgs? dotReceivedWhenSaved = null)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            if (stroke == null)
            {
                throw new ArgumentNullException(nameof(stroke));
            }

            if (stroke.Count == 0)
            {
                return; // Leerer Stroke wird nicht gespeichert
            }

            try
            {
                string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);
                string pageFilePath = GetPageFilePath(normalizedMacAddress, stroke.Section, stroke.Owner, stroke.Note, stroke.Page);

                await _fileSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    // Verzeichnis erstellen, falls nicht vorhanden
                    string? directory = Path.GetDirectoryName(pageFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Bestehende Daten laden oder neue Struktur erstellen
                    PageStrokeData pageData;
                    if (File.Exists(pageFilePath))
                    {
                        try
                        {
                            string jsonContent = await Task.Run(() => File.ReadAllText(pageFilePath)).ConfigureAwait(false);
                            pageData = JsonConvert.DeserializeObject<PageStrokeData>(jsonContent) ?? new PageStrokeData
                            {
                                MacAddress = normalizedMacAddress,
                                DisplayName = displayName,
                                Section = stroke.Section,
                                Owner = stroke.Owner,
                                Note = stroke.Note,
                                Page = stroke.Page
                            };
                        }
                        catch (JsonException ex)
                        {
                            ThreadSafeConsole.WriteLine($"Fehler beim Parsen der Seite {pageFilePath}: {ex.Message}. Erstelle neue Datei.");
                            pageData = new PageStrokeData
                            {
                                MacAddress = normalizedMacAddress,
                                DisplayName = displayName,
                                Section = stroke.Section,
                                Owner = stroke.Owner,
                                Note = stroke.Note,
                                Page = stroke.Page
                            };
                        }
                    }
                    else
                    {
                        pageData = new PageStrokeData
                        {
                            MacAddress = normalizedMacAddress,
                            DisplayName = displayName,
                            Section = stroke.Section,
                            Owner = stroke.Owner,
                            Note = stroke.Note,
                            Page = stroke.Page
                        };
                    }

                    // Geräteinformationen aktualisieren, falls sie sich geändert haben
                    if (string.IsNullOrEmpty(pageData.MacAddress))
                    {
                        pageData.MacAddress = normalizedMacAddress;
                    }
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        pageData.DisplayName = displayName;
                    }

                    // Stroke zu PageStrokeData konvertieren
                    StrokeData strokeData = ConvertStrokeToStrokeData(stroke);
                    pageData.Strokes.Add(strokeData);
                    pageData.LastUpdated = DateTime.UtcNow;

                    // Speichern
                    JsonSerializerSettings settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Include
                    };
                    string jsonContentToSave = JsonConvert.SerializeObject(pageData, settings);
                    await Task.Run(() => File.WriteAllText(pageFilePath, jsonContentToSave)).ConfigureAwait(false);

                    ThreadSafeConsole.WriteLine(
                        $"Stroke erfolgreich gespeichert — Dots={stroke.Count}, Section={stroke.Section}, Owner={stroke.Owner}, Note={stroke.Note}, Page={stroke.Page}, Datei={pageFilePath}");
                    if (dotReceivedWhenSaved != null)
                        ThreadSafeConsole.WriteLine(FormatDotReceivedEventArgsAfterStrokeSaved(dotReceivedWhenSaved));
                }
                finally
                {
                    _fileSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Speichern des Strokes: {ex.Message}");
            }
        }

        /// <summary>
        /// Textdarstellung der SDK-<see cref="DotReceivedEventArgs"/> für die thread-sichere Debug-Konsole (nach persistentem Stroke-Speichern).
        /// </summary>
        private static string FormatDotReceivedEventArgsAfterStrokeSaved(DotReceivedEventArgs args)
        {
            if (args?.Dot == null)
                return "Stroke gespeichert — DotReceivedEventArgs: (kein Dot)";

            Dot d = args.Dot;
            string baseInfo =
                $"Stroke gespeichert — DotReceivedEventArgs: Section={d.Section}, Owner={d.Owner}, Note={d.Note}, Page={d.Page}, " +
                $"DotType={d.DotType}, X={d.X}, Y={d.Y}, Force={d.Force}, Tilt=({d.TiltX},{d.TiltY}), Twist={d.Twist}, " +
                $"Color={d.Color}, Timestamp={d.Timestamp}";

            if (args.ImageProcessingInfo == null)
                return baseInfo;

            ImageProcessingInfo ip = args.ImageProcessingInfo;
            return baseInfo +
                $", ImageProcessingInfo: DotCount={ip.DotCount}, Total={ip.Total}, Processed={ip.Processed}, Success={ip.Success}, Transferred={ip.Transferred}";
        }

        /// <summary>
        /// Speichert mehrere Offline-Strokes.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (normalisiert).</param>
        /// <param name="strokes">Array der zu speichernden Strokes.</param>
        /// <param name="displayName">Optional: DisplayName des Stifts.</param>
        public async Task SaveOfflineStrokesAsync(string macAddress, Stroke[] strokes, string? displayName = null)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            if (strokes == null || strokes.Length == 0)
            {
                return;
            }

            try
            {
                // Strokes nach Seite gruppieren
                var strokesByPage = strokes
                    .Where(s => s != null && s.Count > 0)
                    .GroupBy(s => new { s.Section, s.Owner, s.Note, s.Page })
                    .ToList();

                foreach (var pageGroup in strokesByPage)
                {
                    var pageStrokes = pageGroup.ToList();
                    if (pageStrokes.Count == 0)
                    {
                        continue;
                    }

                    // Ersten Stroke für Seiten-Informationen verwenden
                    Stroke firstStroke = pageStrokes[0];
                    string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);
                    string pageFilePath = GetPageFilePath(normalizedMacAddress, firstStroke.Section, firstStroke.Owner, firstStroke.Note, firstStroke.Page);

                    await _fileSemaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        // Verzeichnis erstellen, falls nicht vorhanden
                        string? directory = Path.GetDirectoryName(pageFilePath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // Bestehende Daten laden oder neue Struktur erstellen
                        PageStrokeData pageData;
                        if (File.Exists(pageFilePath))
                        {
                            try
                            {
                                string jsonContent = await Task.Run(() => File.ReadAllText(pageFilePath)).ConfigureAwait(false);
                                pageData = JsonConvert.DeserializeObject<PageStrokeData>(jsonContent) ?? new PageStrokeData
                                {
                                    MacAddress = normalizedMacAddress,
                                    DisplayName = displayName,
                                    Section = firstStroke.Section,
                                    Owner = firstStroke.Owner,
                                    Note = firstStroke.Note,
                                    Page = firstStroke.Page
                                };
                            }
                            catch (JsonException ex)
                            {
                                ThreadSafeConsole.WriteLine($"Fehler beim Parsen der Seite {pageFilePath}: {ex.Message}. Erstelle neue Datei.");
                                pageData = new PageStrokeData
                                {
                                    MacAddress = normalizedMacAddress,
                                    DisplayName = displayName,
                                    Section = firstStroke.Section,
                                    Owner = firstStroke.Owner,
                                    Note = firstStroke.Note,
                                    Page = firstStroke.Page
                                };
                            }
                        }
                        else
                        {
                            pageData = new PageStrokeData
                            {
                                MacAddress = normalizedMacAddress,
                                DisplayName = displayName,
                                Section = firstStroke.Section,
                                Owner = firstStroke.Owner,
                                Note = firstStroke.Note,
                                Page = firstStroke.Page
                            };
                        }

                        // Geräteinformationen aktualisieren, falls sie sich geändert haben
                        if (string.IsNullOrEmpty(pageData.MacAddress))
                        {
                            pageData.MacAddress = normalizedMacAddress;
                        }
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            pageData.DisplayName = displayName;
                        }

                        // Alle Strokes hinzufügen
                        foreach (Stroke stroke in pageStrokes)
                        {
                            StrokeData strokeData = ConvertStrokeToStrokeData(stroke);
                            pageData.Strokes.Add(strokeData);
                        }

                        pageData.LastUpdated = DateTime.UtcNow;

                        // Speichern
                        JsonSerializerSettings settings = new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Include
                        };
                        string jsonContentToSave = JsonConvert.SerializeObject(pageData, settings);
                        await Task.Run(() => File.WriteAllText(pageFilePath, jsonContentToSave)).ConfigureAwait(false);
                        ThreadSafeConsole.WriteLine(
                            $"Offline-Strokes erfolgreich gespeichert — Strokes={pageStrokes.Count}, Section={firstStroke.Section}, Owner={firstStroke.Owner}, Note={firstStroke.Note}, Page={firstStroke.Page}, Datei={pageFilePath}");
                    }
                    finally
                    {
                        _fileSemaphore.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Speichern der Offline-Strokes: {ex.Message}");
            }
        }

        /// <summary>
        /// Lädt alle Strokes für eine bestimmte Seite.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (normalisiert).</param>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="note">Note Id.</param>
        /// <param name="page">Page Number.</param>
        /// <returns>Liste der Strokes für die angegebene Seite.</returns>
        public async Task<List<Stroke>> GetStrokesAsync(string macAddress, int section, int owner, int note, int page)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            try
            {
                string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);
                string pageFilePath = GetPageFilePath(normalizedMacAddress, section, owner, note, page);

                if (!File.Exists(pageFilePath))
                {
                    return new List<Stroke>();
                }

                await _fileSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    string jsonContent = await Task.Run(() => File.ReadAllText(pageFilePath)).ConfigureAwait(false);
                    PageStrokeData? pageData = JsonConvert.DeserializeObject<PageStrokeData>(jsonContent);

                    if (pageData == null || pageData.Strokes == null || pageData.Strokes.Count == 0)
                    {
                        return new List<Stroke>();
                    }

                    // StrokeData zu Stroke konvertieren
                    List<Stroke> strokes = new List<Stroke>();
                    foreach (StrokeData strokeData in pageData.Strokes)
                    {
                        Stroke stroke = ConvertStrokeDataToStroke(strokeData, section, owner, note, page);
                        strokes.Add(stroke);
                    }

                    return strokes;
                }
                finally
                {
                    _fileSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Laden der Strokes: {ex.Message}");
                return new List<Stroke>();
            }
        }

        /// <summary>
        /// Gibt den vollständigen Pfad zur Page-Datei zurück.
        /// </summary>
        private string GetPageFilePath(string macAddress, int section, int owner, int note, int page)
        {
            // MAC-Adresse für Dateipfade sanitizen (Doppelpunkte entfernen, da Windows diese nicht erlaubt)
            string sanitizedMacAddress = SanitizeMacAddressForPath(macAddress);
            string noteDirectory = Path.Combine(DataDirectoryPath, sanitizedMacAddress, $"{section}_{owner}_{note}");
            return Path.Combine(noteDirectory, $"{page}.json");
        }

        /// <summary>
        /// Sanitized eine MAC-Adresse für die Verwendung in Dateipfaden.
        /// Entfernt Doppelpunkte und andere ungültige Zeichen.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse (normalisiert mit Doppelpunkten).</param>
        /// <returns>Sanitized MAC-Adresse für Dateipfade (z.B. "9C-7B-D2-1A-19-E6").</returns>
        private string SanitizeMacAddressForPath(string macAddress)
        {
            if (string.IsNullOrEmpty(macAddress))
            {
                return macAddress;
            }

            // Doppelpunkte durch Bindestriche ersetzen (Windows-kompatibel)
            return macAddress.Replace(":", "-");
        }

        /// <summary>
        /// Gibt den Buffer-Key für einen bestimmten Stroke zurück.
        /// </summary>
        private string GetBufferKey(string macAddress, int section, int owner, int note, int page)
        {
            return $"{macAddress}_{section}_{owner}_{note}_{page}";
        }

        /// <summary>
        /// Löscht alle lokalen Dateien für einen bestimmten Stift.
        /// </summary>
        /// <param name="macAddress">MAC-Adresse des Stifts (normalisiert).</param>
        /// <returns>Anzahl der gelöschten Dateien.</returns>
        public async Task<int> DeleteAllLocalFilesAsync(string macAddress)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(macAddress))
            {
                throw new ArgumentException("MAC-Adresse darf nicht null oder leer sein.", nameof(macAddress));
            }

            try
            {
                string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress(macAddress);
                string sanitizedMacAddress = SanitizeMacAddressForPath(normalizedMacAddress);
                string penDirectory = Path.Combine(DataDirectoryPath, sanitizedMacAddress);

                if (!Directory.Exists(penDirectory))
                {
                    ThreadSafeConsole.WriteLine($"Keine lokalen Dateien für Stift {normalizedMacAddress} gefunden.");
                    return 0;
                }

                await _fileSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    int deletedCount = 0;

                    // Alle JSON-Dateien rekursiv finden und löschen
                    string[] jsonFiles = Directory.GetFiles(penDirectory, "*.json", SearchOption.AllDirectories);
                    foreach (string filePath in jsonFiles)
                    {
                        try
                        {
                            File.Delete(filePath);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            ThreadSafeConsole.WriteLine($"Fehler beim Löschen der Datei {filePath}: {ex.Message}");
                        }
                    }

                    // Leere Verzeichnisse löschen
                    try
                    {
                        Directory.Delete(penDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Fehler beim Löschen des Verzeichnisses {penDirectory}: {ex.Message}");
                    }

                    ThreadSafeConsole.WriteLine($"Lokale Dateien gelöscht: {deletedCount} Datei(en) für Stift {normalizedMacAddress}.");
                    return deletedCount;
                }
                finally
                {
                    _fileSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Löschen der lokalen Dateien: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Konvertiert einen SDK-Stroke zu StrokeData für die Persistierung.
        /// </summary>
        private StrokeData ConvertStrokeToStrokeData(Stroke stroke)
        {
            StrokeData strokeData = new StrokeData
            {
                Color = stroke.Color,
                TimeStart = stroke.TimeStart,
                TimeEnd = stroke.TimeEnd
            };

            foreach (Dot dot in stroke)
            {
                DotData dotData = new DotData
                {
                    X = dot.X,
                    Y = dot.Y,
                    Force = dot.Force,
                    Timestamp = dot.Timestamp,
                    TiltX = dot.TiltX,
                    TiltY = dot.TiltY,
                    Twist = dot.Twist,
                    Type = dot.DotType,
                    Color = dot.Color
                };
                strokeData.Dots.Add(dotData);
            }

            return strokeData;
        }

        /// <summary>
        /// Konvertiert StrokeData zurück zu einem SDK-Stroke.
        /// </summary>
        private Stroke ConvertStrokeDataToStroke(StrokeData strokeData, int section, int owner, int note, int page)
        {
            Stroke stroke = new Stroke(section, owner, note, page);

            foreach (DotData dotData in strokeData.Dots)
            {
                Dot dot = new Dot(owner, section, note, page, dotData.Timestamp, dotData.X, dotData.Y, dotData.TiltX, dotData.TiltY, dotData.Twist, dotData.Force, dotData.Type, dotData.Color);
                stroke.Add(dot);
            }

            return stroke;
        }

        /// <summary>
        /// Gibt Ressourcen frei.
        /// </summary>
        protected override void DisposeResources()
        {
            _fileSemaphore?.Dispose();
            lock (_dotBufferLock)
            {
                _dotBuffers.Clear();
            }
        }
    }
}
