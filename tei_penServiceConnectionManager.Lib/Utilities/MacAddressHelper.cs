using System;
using System.Text.RegularExpressions;

#nullable enable

namespace TeiPenServiceConnectionManager.Utilities
{
    /// <summary>
    /// Hilfsklasse für die Normalisierung und Formatierung von MAC-Adressen.
    /// </summary>
    public static class MacAddressHelper
    {
        /// <summary>
        /// Normalisiert eine MAC-Adresse in das Format "XX:XX:XX:XX:XX:XX" (großgeschrieben).
        /// Unterstützt sowohl MAC-Adressen mit als auch ohne Doppelpunkte.
        /// </summary>
        /// <param name="macAddress">Die zu normalisierende MAC-Adresse.</param>
        /// <returns>Normalisierte MAC-Adresse im Format "XX:XX:XX:XX:XX:XX" oder die ursprüngliche Zeichenfolge, falls keine gültige MAC-Adresse.</returns>
        public static string NormalizeMacAddress(string macAddress)
        {
            if (string.IsNullOrEmpty(macAddress))
            {
                return macAddress;
            }

            // Entferne alle Doppelpunkte und Leerzeichen
            string cleaned = macAddress.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();

            // Prüfe, ob es eine gültige MAC-Adresse ist (12 Hex-Zeichen)
            if (cleaned.Length == 12 && Regex.IsMatch(cleaned, "^[0-9A-F]{12}$"))
            {
                // Füge Doppelpunkte nach je 2 Zeichen ein
                return $"{cleaned.Substring(0, 2)}:{cleaned.Substring(2, 2)}:{cleaned.Substring(4, 2)}:{cleaned.Substring(6, 2)}:{cleaned.Substring(8, 2)}:{cleaned.Substring(10, 2)}";
            }

            // Falls keine gültige MAC-Adresse, ursprüngliche Zeichenfolge zurückgeben
            return macAddress.ToUpperInvariant();
        }
    }
}

