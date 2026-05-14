using System;
using System.Collections.Generic;
using Neosmartpen.Net;

#nullable enable

namespace TeiPenServiceConnectionManager.Models
{
    /// <summary>
    /// Repräsentiert die gespeicherten Strokes für eine bestimmte Seite.
    /// </summary>
    public class PageStrokeData
    {
        /// <summary>
        /// MAC-Adresse des Stifts, der diese Daten erstellt hat.
        /// </summary>
        public string? MacAddress { get; set; }

        /// <summary>
        /// DisplayName des Stifts (falls gesetzt).
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Section Id des NCode-Papiers.
        /// </summary>
        public int Section { get; set; }

        /// <summary>
        /// Owner Id des NCode-Papiers.
        /// </summary>
        public int Owner { get; set; }

        /// <summary>
        /// Note Id des NCode-Papiers.
        /// </summary>
        public int Note { get; set; }

        /// <summary>
        /// Page Number des NCode-Papiers.
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Liste aller Strokes für diese Seite.
        /// </summary>
        public List<StrokeData> Strokes { get; set; } = new List<StrokeData>();

        /// <summary>
        /// Zeitpunkt der letzten Aktualisierung.
        /// </summary>
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Repräsentiert einen einzelnen Stroke mit allen zugehörigen Dots.
    /// </summary>
    public class StrokeData
    {
        /// <summary>
        /// Farbe des Strokes.
        /// </summary>
        public int Color { get; set; }

        /// <summary>
        /// Timestamp des Startpunkts.
        /// </summary>
        public long TimeStart { get; set; }

        /// <summary>
        /// Timestamp des Endpunkts.
        /// </summary>
        public long TimeEnd { get; set; }

        /// <summary>
        /// Liste aller Dots, die zu diesem Stroke gehören.
        /// </summary>
        public List<DotData> Dots { get; set; } = new List<DotData>();
    }

    /// <summary>
    /// Repräsentiert einen einzelnen Dot mit allen Koordinaten- und Sensordaten.
    /// </summary>
    public class DotData
    {
        /// <summary>
        /// X-Koordinate des NCode-Cells.
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Y-Koordinate des NCode-Cells.
        /// </summary>
        public float Y { get; set; }

        /// <summary>
        /// Druck (Force) des Dots.
        /// </summary>
        public int Force { get; set; }

        /// <summary>
        /// Timestamp des Dots.
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// Tilt X des Stifts.
        /// </summary>
        public int TiltX { get; set; }

        /// <summary>
        /// Tilt Y des Stifts.
        /// </summary>
        public int TiltY { get; set; }

        /// <summary>
        /// Twist (Drehung) des Stifts.
        /// </summary>
        public int Twist { get; set; }

        /// <summary>
        /// Typ des Dots (PEN_DOWN, PEN_MOVE, PEN_UP, etc.).
        /// </summary>
        public DotTypes Type { get; set; }

        /// <summary>
        /// Farbe des Dots.
        /// </summary>
        public int Color { get; set; }
    }
}
