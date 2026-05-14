namespace tei_penService_ui.Models
{
    /// <summary>
    /// Ein Eintrag auf der Desktop-Arbeitsfläche (eine Ebene: Unterordner oder Notizdatei).
    /// </summary>
    public sealed class WorkspaceDesktopItemInfo
    {
        /// <summary>Absoluter Pfad zu Ordner oder Datei.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Anzeigename.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>True für Verzeichnisse.</summary>
        public bool IsFolder { get; set; }
    }
}
