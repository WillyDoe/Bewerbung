namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Stift-Control für "Verfügbare Geräte": RSSI, bei Hover/Klick Verbinden/Passwort/Trennen.
    /// Nutzung des einheitlichen PenControl mit DisplayMode = Available.
    /// </summary>
    public sealed class StandardPenControl : PenControl
    {
        public StandardPenControl()
        {
            DisplayMode = PenControlDisplayMode.Available;
        }
    }
}
