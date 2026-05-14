namespace tei_penService_ui.Models
{
    /// <summary>
    /// Zentrale Konstanten für den Workspace-Dateibaum und Notiz-Dateien (keine Magic Strings streuen).
    /// </summary>
    public static class WorkspaceConstants
    {
        /// <summary>Ordner unter dem Anwendungsbasisverzeichnis.</summary>
        public const string WorkspaceDirectoryName = "workspace";

        /// <summary>Persistente Stift-Strokes unter dem Anwendungsbasisverzeichnis (Kalibrierung, Offline).</summary>
        public const string PenStrokeDataDirectoryName = "data";

        /// <summary>Endung für eine Notiz-JSON-Datei (inkl. Punkt).</summary>
        public const string NoteFileExtension = ".tei.json";

        /// <summary>Standard-Unterordner beim ersten Start.</summary>
        public static readonly string[] DefaultFolderNames = { "Deutsch", "Mathe", "Englisch" };

        /// <summary>Aktuelles JSON-Schema für <see cref="WorkspaceInkDocumentPayload"/>.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Datei unter dem Workspace-Stamm für Icon-Positionen der Desktop-Ansicht.</summary>
        public const string DesktopLayoutFileName = "desktop_layout.json";

        /// <summary>JSON-Schema für <see cref="WorkspaceDesktopLayoutFile"/>.</summary>
        public const int DesktopLayoutSchemaVersion = 1;
    }

    /// <summary>
    /// Physische Papiergrößen → logische WPF-Pixel (96 dpi): eine digitale „Seite“ entspricht einer A5-Fläche.
    /// </summary>
    public static class WorkspacePaperLayout
    {
        public const double A5WidthMm = 148.0;
        public const double A5HeightMm = 210.0;

        /// <summary>mm → geräteunabhängige Pixel (wie WPF für typografische Maße üblich).</summary>
        public static double MmToDpiLogicalPx(double mm) => mm * 96.0 / 25.4;

        public static double A5WidthPx => MmToDpiLogicalPx(A5WidthMm);
        public static double A5HeightPx => MmToDpiLogicalPx(A5HeightMm);
    }
}
