using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Ink;
using tei_penService_ui.Models;

namespace tei_penService_ui.Services
{
    /// <summary>
    /// Zentrale Hilfen für ISF ↔ Base64 und <see cref="StrokeCollection"/> (DRY für Speicherung und Tabs).
    /// </summary>
    public static class WorkspaceInkSerialization
    {
        /// <summary>Serialisiert alle Striche als ISF und gibt Base64 zurück (leerer Canvas → leerer String).</summary>
        public static string StrokesToIsfBase64(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0)
                return string.Empty;
            try
            {
                using (var ms = new MemoryStream())
                {
                    strokes.Save(ms);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspaceInkSerialization.StrokesToIsfBase64: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>Lädt ISF aus Base64 in eine neue <see cref="StrokeCollection"/>.</summary>
        public static StrokeCollection StrokesFromIsfBase64(string base64)
        {
            var collection = new StrokeCollection();
            if (string.IsNullOrWhiteSpace(base64))
                return collection;
            try
            {
                byte[] raw = Convert.FromBase64String(base64);
                using (var ms = new MemoryStream(raw))
                {
                    collection = new StrokeCollection(ms);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspaceInkSerialization.StrokesFromIsfBase64: {ex.Message}");
            }
            return collection;
        }

        /// <summary>Überträgt Striche auf den Canvas (ersetzt vorhandene Striche); ISF-Roundtrip als Kopie.</summary>
        public static void ApplyStrokesToCanvas(System.Windows.Controls.InkCanvas canvas, StrokeCollection strokes)
        {
            if (canvas == null)
                return;
            canvas.Strokes.Clear();
            if (strokes == null || strokes.Count == 0)
                return;
            try
            {
                using (var ms = new MemoryStream())
                {
                    strokes.Save(ms);
                    ms.Position = 0;
                    var copy = new StrokeCollection(ms);
                    foreach (Stroke s in copy)
                        canvas.Strokes.Add(s);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceInkSerialization.ApplyStrokesToCanvas: {ex.Message}");
            }
        }

        /// <summary>Liefert die rechte untere Ecke der Strich-Bounding-Box in Pixeln (für Canvas-Breite).</summary>
        public static double GetStrokeBoundsMaxX(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0)
                return 0;
            try
            {
                var b = strokes.GetBounds();
                return b.Right;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspaceInkSerialization.GetStrokeBoundsMaxX: {ex.Message}");
                return 0;
            }
        }

        /// <summary>Untere Kante der Strich-Bounding-Box (für Canvas-Höhe).</summary>
        public static double GetStrokeBoundsMaxY(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0)
                return 0;
            try
            {
                var b = strokes.GetBounds();
                return b.Bottom;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspaceInkSerialization.GetStrokeBoundsMaxY: {ex.Message}");
                return 0;
            }
        }
    }
}
