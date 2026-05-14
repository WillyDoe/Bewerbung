namespace tei_penService_ui.Models
{
    /// <summary>
    /// Laufzeit-Zustand einer geöffneten Notiz (Browser-Tab); ISF und Text werden beim Tab-Wechsel mit dem Canvas synchronisiert.
    /// </summary>
    public sealed class WorkspaceDocumentTabState
    {
        /// <summary>Voller Pfad zur <c>.tei.json</c>-Datei.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Kurztitel für die Tab-Leiste.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Zwischengespeicherter Freihand-Inhalt (ISF als Base64), wenn der Tab nicht aktiv ist.</summary>
        public string CachedIsfBase64 { get; set; } = string.Empty;

        /// <summary>Aktueller erkannter Gesamttext (gefiltert), UTF-8-kompatibel als .NET-String.</summary>
        public string RecognizedTextUtf8 { get; set; } = string.Empty;
    }
}
