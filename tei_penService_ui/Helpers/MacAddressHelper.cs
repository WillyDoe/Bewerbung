using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Models;
using tei_penService_ui.Models;

namespace tei_penService_ui.Helpers
{
    /// <summary>
    /// Zentralisierte MAC-Adress-Normalisierung für Vergleiche über PenInformation, ConnectedPenDisplayInfo und PairedPenDisplayInfo.
    /// </summary>
    public static class MacAddressHelper
    {
        /// <summary>
        /// Normalisiert die MAC-Adresse für Vergleiche (PenInformation, ConnectedPenDisplayInfo oder PairedPenDisplayInfo).
        /// Gibt null zurück, wenn keine MAC extrahiert werden kann.
        /// </summary>
        public static string NormalizeMacAddress(object penDisplay)
        {
            if (penDisplay == null)
                return null;
            if (penDisplay is PenInformation pi)
            {
                string mac = pi.MacAddress;
                if (string.IsNullOrEmpty(mac))
                    mac = pi.Id;
                return string.IsNullOrEmpty(mac) ? null : mac.ToUpperInvariant();
            }
            if (penDisplay is ConnectedPenDisplayInfo c)
            {
                string mac = c.MacAddress;
                if (string.IsNullOrEmpty(mac))
                    mac = c.Id;
                return string.IsNullOrEmpty(mac) ? null : mac.ToUpperInvariant();
            }
            if (penDisplay is PairedPenDisplayInfo p)
            {
                string mac = p.MacAddress;
                return string.IsNullOrEmpty(mac) ? null : mac.ToUpperInvariant();
            }
            return null;
        }
    }
}
