using TeiPenServiceConnectionManager.Models;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Anzeige-Daten für einen gekoppelten Stift (aus memory.json).
    /// Wird in der Stiftliste unter "Gekoppelte Geräte" verwendet.
    /// IsInRange und Rssi kommen aus der aktuellen Gerätesuche (Live), nicht aus memory.
    /// </summary>
    public class PairedPenDisplayInfo
    {
        public string Name { get; }
        public string MacAddress { get; }
        /// <summary>Live-RSSI wenn der Stift aktuell in Reichweite ist, sonst 0.</summary>
        public int Rssi { get; }
        /// <summary>True, wenn der Stift aktuell bei der Gerätesuche in Reichweite ist.</summary>
        public bool IsInRange { get; }

        /// <summary>
        /// Erstellt Anzeige-Daten für einen gekoppelten Stift. IsInRange und liveRssi stammen aus der aktuellen Discovered-Liste.
        /// </summary>
        /// <param name="entry">Eintrag aus memory.json.</param>
        /// <param name="isInRange">True, wenn der Stift gerade in der Gerätesuche sichtbar ist.</param>
        /// <param name="liveRssi">Aktueller RSSI-Wert von der Gerätesuche (nur relevant wenn isInRange).</param>
        public PairedPenDisplayInfo(PenMemoryEntry entry, bool isInRange = false, int liveRssi = 0)
        {
            if (entry == null)
            {
                Name = string.Empty;
                MacAddress = string.Empty;
                Rssi = 0;
                IsInRange = false;
                return;
            }
            Name = !string.IsNullOrEmpty(entry.PenName) ? entry.PenName : (entry.DisplayName ?? string.Empty);
            MacAddress = entry.MacAddress ?? string.Empty;
            IsInRange = isInRange;
            Rssi = isInRange ? liveRssi : 0;
        }
    }
}
