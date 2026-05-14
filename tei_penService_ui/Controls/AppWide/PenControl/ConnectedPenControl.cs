namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Stift-Control für den verbundenen Stift (nur ein Eintrag in "Verfügbare Geräte"): "Verbunden", Hover: Trennen.
    /// Nutzung des einheitlichen PenControl mit DisplayMode = Connected.
    /// </summary>
    public sealed class ConnectedPenControl : PenControl
    {
        public ConnectedPenControl()
        {
            DisplayMode = PenControlDisplayMode.Connected;
        }
    }
}
