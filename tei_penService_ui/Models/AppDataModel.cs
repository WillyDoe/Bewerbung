using System.Collections.Generic;
using TeiPenServiceConnectionManager.Models;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Zentrales Modell für alle App-Daten (Users + PairedPens).
    /// </summary>
    public class AppDataModel
    {
        /// <summary>
        /// Benutzer, Key = Email (lowercase).
        /// </summary>
        public Dictionary<string, UserMemoryEntry> Users { get; set; } = new Dictionary<string, UserMemoryEntry>();

        /// <summary>
        /// Gekoppelte Stifte (aus Lib synchronisiert), Key = MAC-Adresse.
        /// </summary>
        public Dictionary<string, PenMemoryEntry> PairedPens { get; set; } = new Dictionary<string, PenMemoryEntry>();
    }
}
