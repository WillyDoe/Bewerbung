namespace tei_penService_ui.Models
{
    /// <summary>
    /// Achsparallelles Rechteck der Papierfläche in NCode-Zellen (Kalibrierung: vier Randstriche → Min/Max X/Y).
    /// </summary>
    public struct PaperBoundsNCodeCells
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;
    }
}
