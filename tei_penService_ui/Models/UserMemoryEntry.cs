using System;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Eintrag für einen Benutzer in der App-Daten.
    /// </summary>
    public class UserMemoryEntry
    {
        /// <summary>
        /// Eindeutige ID des Benutzers (Guid).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// E-Mail-Adresse (eindeutig, Anmelde-ID).
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Anzeigename des Benutzers.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Passwort (Klartext für Mocking).
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// MAC-Adresse des verknüpften Stifts (optional, für passwortlosen Zugang).
        /// </summary>
        public string LinkedPenMacAddress { get; set; }

        /// <summary>
        /// Zeitpunkt der Registrierung.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
