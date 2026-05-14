namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Stift-Control für "Gekoppelte Geräte": RSSI / Verbunden / nicht in Reichweite; Hover: Entfernen, Verbinden, Trennen.
    /// Nutzung des einheitlichen PenControl mit DisplayMode = Paired.
    /// </summary>
    public sealed class PairedPenControl : PenControl
    {
        public PairedPenControl()
        {
            DisplayMode = PenControlDisplayMode.Paired;
        }
    }
}
