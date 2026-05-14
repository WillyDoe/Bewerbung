using System;
using System.Collections.Generic;

#nullable enable

namespace TeiPenServiceConnectionManager.Models
{
    /// <summary>
    /// Eintrag für einen gekoppelten Stift in der Memory-Datei.
    /// </summary>
    public class PenMemoryEntry
    {
        /// <summary>
        /// MAC-Adresse des Stifts (normalisiert, Primärschlüssel).
        /// </summary>
        public string MacAddress { get; set; } = string.Empty;

        /// <summary>
        /// Windows Device ID des Stifts.
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Name des Stifts (aus SDK: PenInformation.Name).
        /// </summary>
        public string PenName { get; set; } = string.Empty;

        /// <summary>
        /// Anzeigename des Stifts (aus Windows.DeviceInformation: DisplayName, z.B. "NWP-F45").
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Protokollversion des Stifts (aus SDK: PenInformation.Protocol).
        /// </summary>
        public int Protocol { get; set; }

        /// <summary>
        /// Zeitpunkt der ersten erfolgreichen Verbindung.
        /// </summary>
        public DateTime FirstConnectedAt { get; set; }

        /// <summary>
        /// Zeitpunkt der letzten erfolgreichen Verbindung.
        /// </summary>
        public DateTime LastConnectedAt { get; set; }

        /// <summary>
        /// Gerätepasswort des Stifts (optional, wird nur gespeichert wenn gesetzt und nicht "0000").
        /// </summary>
        public string? Password { get; set; }
    }

    /// <summary>
    /// Modell für die persistente Speicherung von gekoppelten Stiften.
    /// </summary>
    public class TeiPenServiceMemoryModel
    {
        /// <summary>
        /// Dictionary aller gekoppelten Stifte, wobei der Key die normalisierte MAC-Adresse ist.
        /// </summary>
        public Dictionary<string, PenMemoryEntry> PairedPens { get; set; } = new Dictionary<string, PenMemoryEntry>();
    }
}
