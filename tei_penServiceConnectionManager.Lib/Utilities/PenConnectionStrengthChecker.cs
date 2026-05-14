using Neosmartpen.Net.Bluetooth;

namespace TeiPenServiceConnectionManager.Utilities
{
    /// <summary>
    /// Prüft die Verbindungsstärke eines Smartpens.
    /// </summary>
    public static class PenConnectionStrengthChecker
    {
        private const int MIN_RSSI = -90;
        private const int INVALID_RSSI_THRESHOLD = -100;


        /// <summary>
        /// Prüft, ob die RSSI-Wert gültig ist.
        /// </summary>
        /// <param name="rssi">Die RSSI-Wert des Stifts.</param>
        /// <returns>True, wenn der RSSI-Wert gültig ist, false wenn unter -100 dBm.</returns>
        public static bool IsRssiValid(int rssi)
        {
            return rssi >= INVALID_RSSI_THRESHOLD;
        }

        /// <summary>
        /// Prüft, ob der Stift noch in Reichweite ist.
        /// </summary>
        /// <param name="penInformation">Die PenInformation des Stifts.</param>
        /// <returns>True, wenn der Stift noch in Reichweite ist, false sonst. Reichweite: -90 dBm </returns>
        public static bool PenIsStillInRange(PenInformation penInformation) 
        {
            // Wenn RSSI ungültig, dann wird der Stift als noch in Reichweite betrachtet.
            // Dies ist notwendig, weil die Neosmartpens alle 2-3 Sekunden ein Update mit einem ungültigen RSSI-Wert senden.
            if (!IsRssiValid(penInformation.Rssi))
            {
                return true;
            }

           if (penInformation.Rssi < MIN_RSSI)
           {
                return false;
           }

           return true;
        }
    }
}