namespace tei_penService_ui.Models
{
    /// <summary>
    /// Geschätztes Notizbuch-Format aus beobachteten Stiftkoordinaten (NCode-Zellen und mm).
    /// </summary>
    public sealed class NotebookFormatEstimate
    {
        /// <summary>Notizbuch-Schlüssel Section|Owner|Note.</summary>
        public string BookKey { get; set; } = "";

        /// <summary>Erkannte oder nächstliegende Standardbezeichnung (z. B. A4, A5).</summary>
        public string StandardFormatLabel { get; set; } = "";

        /// <summary>Geschätzte Seitenbreite in NCode-Zellen (nominal oder aus Median).</summary>
        public float WidthCells { get; set; }

        /// <summary>Geschätzte Seitenhöhe in NCode-Zellen.</summary>
        public float HeightCells { get; set; }

        public float WidthMm { get; set; }
        public float HeightMm { get; set; }

        /// <summary>0–1: wie gut die Mediane zu einem Standardformat passen und wie viele Seiten belegt sind.</summary>
        public float Confidence { get; set; }

        /// <summary>Anzahl der Seiten mit mindestens einem Dot.</summary>
        public int SamplePageCount { get; set; }

        /// <summary>Verwendeter horizontaler Stride in NCode-Zellen (Seitenabstand).</summary>
        public float HorizontalStrideCells { get; set; }

        /// <summary>True, wenn das Standardformat über Seitenverhältnis + Größe gewählt wurde (nicht nur Fallback).</summary>
        public bool UsedStandardFormatMatch { get; set; }
    }
}
