using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Utilities;
using tei_penService_ui.Models;

namespace tei_penService_ui.Utilities
{
    /// <summary>
    /// Liest Kalibrierungs-Seiten aus dem Pen-Datenordner: eine Seite mit genau vier gespeicherten Strichen
    /// (Randmarken) liefert die NCode-Bounding-Box der Papierfläche.
    /// </summary>
    public static class PenCalibrationPageLoader
    {
        /// <summary>Anzahl Striche auf einer Kalibrierungsseite (vier Randmarken).</summary>
        public const int CalibrationStrokeCount = 4;

        /// <summary>
        /// Sucht unter allen Notizordnern die aktuellste Seite mit vier Strichen und liefert deren Dot-Bounding-Box.
        /// </summary>
        public static bool TryLoadBestCalibrationBounds(string applicationBaseDirectory, string macAddressRaw, out PaperBoundsNCodeCells bounds)
        {
            bounds = default;
            if (string.IsNullOrWhiteSpace(applicationBaseDirectory) || string.IsNullOrWhiteSpace(macAddressRaw))
                return false;

            string normalizedMac = MacAddressHelper.NormalizeMacAddress(macAddressRaw);
            if (string.IsNullOrEmpty(normalizedMac))
                return false;

            string sanitizedMac = normalizedMac.Replace(":", "-");
            string penRoot = Path.Combine(applicationBaseDirectory, WorkspaceConstants.PenStrokeDataDirectoryName, sanitizedMac);
            if (!Directory.Exists(penRoot))
                return false;

            DateTime bestTime = DateTime.MinValue;
            PaperBoundsNCodeCells bestBounds = default;
            bool found = false;

            foreach (string noteDir in Directory.GetDirectories(penRoot))
            {
                foreach (string jsonPath in Directory.GetFiles(noteDir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string json = File.ReadAllText(jsonPath);
                        PageStrokeData page = JsonConvert.DeserializeObject<PageStrokeData>(json);
                        if (page?.Strokes == null || page.Strokes.Count != CalibrationStrokeCount)
                            continue;

                        if (!TryComputeDotBounds(page, out PaperBoundsNCodeCells b))
                            continue;

                        DateTime t = page.LastUpdated;
                        if (!found || t >= bestTime)
                        {
                            found = true;
                            bestTime = t;
                            bestBounds = b;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"PenCalibrationPageLoader: {jsonPath}: {ex.Message}");
                    }
                }
            }

            if (!found)
                return false;

            bounds = bestBounds;
            float dx = bounds.MaxX - bounds.MinX;
            float dy = bounds.MaxY - bounds.MinY;
            return dx > 1e-3f && dy > 1e-3f;
        }

        private static bool TryComputeDotBounds(PageStrokeData page, out PaperBoundsNCodeCells bounds)
        {
            bounds = default;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            foreach (StrokeData stroke in page.Strokes.Where(s => s != null && s.Dots != null))
            {
                foreach (DotData d in stroke.Dots)
                {
                    if (d.X < minX) minX = d.X;
                    if (d.X > maxX) maxX = d.X;
                    if (d.Y < minY) minY = d.Y;
                    if (d.Y > maxY) maxY = d.Y;
                }
            }

            if (minX == float.MaxValue || minY == float.MaxValue)
                return false;

            bounds = new PaperBoundsNCodeCells
            {
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY
            };
            return true;
        }
    }
}
