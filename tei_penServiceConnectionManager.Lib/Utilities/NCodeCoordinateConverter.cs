using System;
using System.Collections.Generic;
using System.Linq;
using Neosmartpen.Net;

#nullable enable

namespace TeiPenServiceConnectionManager.Utilities
{
    /// <summary>
    /// Utility-Klasse für die Konvertierung von NCode-Koordinaten zu Pixel-Koordinaten
    /// und für die Berechnung von Page-Formaten für die digitale Darstellung.
    /// </summary>
    public static class NCodeCoordinateConverter
    {
        /// <summary>
        /// Größe einer NCode-Cell in Millimetern (2.371mm).
        /// </summary>
        public const float NCodeCellSizeMm = 2.371f;

        /// <summary>
        /// Standard DPI für die Darstellung (96 DPI = Windows Standard).
        /// Kann später aus der UI gesetzt werden, sodass die Berechnung dynamisch je nach Framework und Skalierung gesetzt werden kann.
        /// </summary>
        public const float DefaultDpi = 96.0f;

        /// <summary>
        /// Konvertiert NCode-Koordinaten (in Cells) zu Millimetern.
        /// </summary>
        /// <param name="ncodeCoordinate">NCode-Koordinate in Cells.</param>
        /// <returns>Koordinate in Millimetern.</returns>
        public static float NCodeToMillimeters(float ncodeCoordinate)
        {
            return ncodeCoordinate * NCodeCellSizeMm;
        }

        /// <summary>
        /// Konvertiert Millimeter zu Pixel bei gegebener DPI.
        /// </summary>
        /// <param name="millimeters">Wert in Millimetern.</param>
        /// <param name="dpi">DPI (Dots Per Inch) für die Konvertierung. Standard: 96 DPI.</param>
        /// <returns>Wert in Pixeln.</returns>
        public static float MillimetersToPixels(float millimeters, float dpi = DefaultDpi)
        {
            // 1 Inch = 25.4mm
            // Pixel = (Millimeter / 25.4) * DPI
            return (millimeters / 25.4f) * dpi;
        }

        /// <summary>
        /// Konvertiert NCode-Koordinaten direkt zu Pixel-Koordinaten.
        /// </summary>
        /// <param name="ncodeCoordinate">NCode-Koordinate in Cells.</param>
        /// <param name="dpi">DPI für die Konvertierung. Standard: 96 DPI.</param>
        /// <returns>Koordinate in Pixeln.</returns>
        public static float NCodeToPixels(float ncodeCoordinate, float dpi = DefaultDpi)
        {
            float millimeters = NCodeToMillimeters(ncodeCoordinate);
            return MillimetersToPixels(millimeters, dpi);
        }

        /// <summary>
        /// Berechnet die Bounding Box (min/max Koordinaten) für eine Liste von Strokes.
        /// </summary>
        /// <param name="strokes">Liste von Strokes.</param>
        /// <returns>Bounding Box mit Min/Max X/Y Koordinaten in NCode-Cells.</returns>
        public static NCodeBoundingBox CalculateBoundingBox(IEnumerable<Stroke> strokes)
        {
            if (strokes == null || !strokes.Any())
            {
                return new NCodeBoundingBox();
            }

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            foreach (Stroke stroke in strokes)
            {
                foreach (Dot dot in stroke)
                {
                    minX = Math.Min(minX, dot.X);
                    minY = Math.Min(minY, dot.Y);
                    maxX = Math.Max(maxX, dot.X);
                    maxY = Math.Max(maxY, dot.Y);
                }
            }

            return new NCodeBoundingBox
            {
                MinX = minX == float.MaxValue ? 0 : minX,
                MinY = minY == float.MaxValue ? 0 : minY,
                MaxX = maxX == float.MinValue ? 0 : maxX,
                MaxY = maxY == float.MinValue ? 0 : maxY
            };
        }

        /// <summary>
        /// Berechnet die Page-Dimensionen (Breite und Höhe) für eine Liste von Strokes.
        /// </summary>
        /// <param name="strokes">Liste von Strokes.</param>
        /// <param name="dpi">DPI für die Konvertierung. Standard: 96 DPI.</param>
        /// <returns>Page-Dimensionen in Pixeln und Millimetern.</returns>
        public static PageDimensions CalculatePageDimensions(IEnumerable<Stroke> strokes, float dpi = DefaultDpi)
        {
            NCodeBoundingBox boundingBox = CalculateBoundingBox(strokes);

            float widthNCode = boundingBox.MaxX - boundingBox.MinX;
            float heightNCode = boundingBox.MaxY - boundingBox.MinY;

            return new PageDimensions
            {
                WidthPixels = NCodeToPixels(widthNCode, dpi),
                HeightPixels = NCodeToPixels(heightNCode, dpi),
                WidthMm = NCodeToMillimeters(widthNCode),
                HeightMm = NCodeToMillimeters(heightNCode),
                MinX = boundingBox.MinX,
                MinY = boundingBox.MinY,
                MaxX = boundingBox.MaxX,
                MaxY = boundingBox.MaxY
            };
        }

        /// <summary>
        /// Konvertiert einen Dot von NCode-Koordinaten zu Pixel-Koordinaten.
        /// </summary>
        /// <param name="dot">Der zu konvertierende Dot.</param>
        /// <param name="dpi">DPI für die Konvertierung. Standard: 96 DPI.</param>
        /// <param name="offsetX">X-Offset in Pixeln (optional, für Zentrierung).</param>
        /// <param name="offsetY">Y-Offset in Pixeln (optional, für Zentrierung).</param>
        /// <returns>Dot mit Pixel-Koordinaten.</returns>
        public static PixelDot ConvertDotToPixels(Dot dot, float dpi = DefaultDpi, float offsetX = 0, float offsetY = 0)
        {
            return new PixelDot
            {
                X = NCodeToPixels(dot.X, dpi) + offsetX,
                Y = NCodeToPixels(dot.Y, dpi) + offsetY,
                OriginalDot = dot
            };
        }

        /// <summary>
        /// Konvertiert einen Stroke von NCode-Koordinaten zu Pixel-Koordinaten.
        /// </summary>
        /// <param name="stroke">Der zu konvertierende Stroke.</param>
        /// <param name="dpi">DPI für die Konvertierung. Standard: 96 DPI.</param>
        /// <param name="offsetX">X-Offset in Pixeln (optional, für Zentrierung).</param>
        /// <param name="offsetY">Y-Offset in Pixeln (optional, für Zentrierung).</param>
        /// <returns>Liste von PixelDots.</returns>
        public static List<PixelDot> ConvertStrokeToPixels(Stroke stroke, float dpi = DefaultDpi, float offsetX = 0, float offsetY = 0)
        {
            List<PixelDot> pixelDots = new List<PixelDot>();
            foreach (Dot dot in stroke)
            {
                pixelDots.Add(ConvertDotToPixels(dot, dpi, offsetX, offsetY));
            }
            return pixelDots;
        }
    }

    /// <summary>
    /// Repräsentiert eine Bounding Box in NCode-Koordinaten.
    /// </summary>
    public class NCodeBoundingBox
    {
        public float MinX { get; set; }
        public float MinY { get; set; }
        public float MaxX { get; set; }
        public float MaxY { get; set; }

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
    }

    /// <summary>
    /// Repräsentiert die Dimensionen einer Seite in verschiedenen Einheiten.
    /// </summary>
    public class PageDimensions
    {
        /// <summary>
        /// Breite in Pixeln.
        /// </summary>
        public float WidthPixels { get; set; }

        /// <summary>
        /// Höhe in Pixeln.
        /// </summary>
        public float HeightPixels { get; set; }

        /// <summary>
        /// Breite in Millimetern.
        /// </summary>
        public float WidthMm { get; set; }

        /// <summary>
        /// Höhe in Millimetern.
        /// </summary>
        public float HeightMm { get; set; }

        /// <summary>
        /// Minimale X-Koordinate in NCode-Cells.
        /// </summary>
        public float MinX { get; set; }

        /// <summary>
        /// Minimale Y-Koordinate in NCode-Cells.
        /// </summary>
        public float MinY { get; set; }

        /// <summary>
        /// Maximale X-Koordinate in NCode-Cells.
        /// </summary>
        public float MaxX { get; set; }

        /// <summary>
        /// Maximale Y-Koordinate in NCode-Cells.
        /// </summary>
        public float MaxY { get; set; }
    }

    /// <summary>
    /// Repräsentiert einen Dot mit Pixel-Koordinaten.
    /// </summary>
    public class PixelDot
    {
        /// <summary>
        /// X-Koordinate in Pixeln.
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Y-Koordinate in Pixeln.
        /// </summary>
        public float Y { get; set; }

        /// <summary>
        /// Original Dot mit allen NCode-Informationen.
        /// </summary>
        public Dot? OriginalDot { get; set; }
    }
}
