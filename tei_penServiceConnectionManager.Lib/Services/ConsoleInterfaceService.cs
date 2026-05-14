using System;
using System.Collections.Generic;
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Utilities;
using TeiPenServiceConnectionManager.Models;

#nullable enable
namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Verwaltet die UI-Darstellung für die Console-Anwendung.
    /// </summary>
    public static class ConsoleInterfaceService
    {
        /// <summary>
        /// Zeigt nur die Verbindungs-spezifischen Optionen an (wird automatisch nach Verbindung angezeigt).
        /// </summary>
        /// <param name="connectionState">Der aktuelle Verbindungsstatus des Stifts.</param>
        public static void DisplayConnectionOptions(PenConnectionState connectionState)
        {
            // Nur anzeigen, wenn vollständig authentifiziert
            if (connectionState != PenConnectionState.Authenticated)
            {
                return;
            }

            ThreadSafeConsole.WriteLine("\nVerbindungsoptionen:");
            ThreadSafeConsole.WriteLine("13. Batteriestatus abfragen [battery] oder [bat]");
            ThreadSafeConsole.WriteLine("14. DisplayName setzen [setname <name>]");
            ThreadSafeConsole.WriteLine("15. Passwort setzen [setpassword <old> <new>]");
            ThreadSafeConsole.WriteLine("    Hinweis: Verwenden Sie \"\" (leeres String) als <old>, wenn noch kein Passwort gesetzt wurde.");
            ThreadSafeConsole.WriteLine("16. Passwort entfernen [removepassword <current>]");
            ThreadSafeConsole.WriteLine("17. Alle Notizen des Stifts löschen [deleteallnotes]");
        }

        /// <summary>
        /// Zeigt alle verfügbaren Aktionen an.
        /// </summary>
        /// <param name="connectionState">Der aktuelle Verbindungsstatus des Stifts.</param>
        public static void DisplayMenu(PenConnectionState connectionState = PenConnectionState.Disconnected)
        {
            ThreadSafeConsole.WriteLine("\nBitte wählen Sie eine Aktion aus:");
            ThreadSafeConsole.WriteLine("1. Suche nach deinem tei-Pen starten [s]");
            ThreadSafeConsole.WriteLine("2. Suche nach deinem tei-Pen beenden [b]");
            ThreadSafeConsole.WriteLine("3. Verbinde mit deinem tei-Pen [c <mac-address>]");
            ThreadSafeConsole.WriteLine("4. Trenne die aktive Verbindung zu deinem tei-Pen [d]");
            ThreadSafeConsole.WriteLine("5. Bluetoothverwaltungsinterface beenden [q]");
            ThreadSafeConsole.WriteLine("6. Bluetooth aktivieren/deaktivieren [bt]");
            ThreadSafeConsole.WriteLine("7. Stifte in Reichweite anzeigen [p]");
            ThreadSafeConsole.WriteLine("8. Verbundenen tei-Pen anzeigen [l]");
            ThreadSafeConsole.WriteLine("9. Gekoppelte tei-Pens anzeigen [k]");
            ThreadSafeConsole.WriteLine("10. Gekoppelte tei-Pens entfernen [r]");
            ThreadSafeConsole.WriteLine("11. Bestimmten gekoppelten tei-Pen entfernen [r <mac-address>]");
            ThreadSafeConsole.WriteLine("12. Aktionen anzeigen [a]");
            
            // Nur anzeigen, wenn ein Stift vollständig authentifiziert ist
            if (connectionState == PenConnectionState.Authenticated)
            {
                ThreadSafeConsole.WriteLine("13. Batteriestatus abfragen [battery] oder [bat]");
                ThreadSafeConsole.WriteLine("14. DisplayName setzen [setname <name>]");
                ThreadSafeConsole.WriteLine("15. Passwort setzen [setpassword <old> <new>]");
                ThreadSafeConsole.WriteLine("    Hinweis: Verwenden Sie \"\" (leeres String) als <old>, wenn noch kein Passwort gesetzt wurde.");
                ThreadSafeConsole.WriteLine("16. Passwort entfernen [removepassword <current>]");
                ThreadSafeConsole.WriteLine("17. Alle Notizen des Stifts löschen [deleteallnotes]");
            }
        }   

        /// <summary>
        /// Zeigt alle gefundenen Stifte an.
        /// </summary>
        public static void DisplayDiscoveredPens(IReadOnlyCollection<PenInformation> discoveredPens, Func<string, bool>? isConnectedCallback = null)
        {
            if (discoveredPens.Count == 0)
            {
                ThreadSafeConsole.WriteLine("Keine tei-Pens in Reichweite verfügbar.");
                return;
            }

            const int noColumnWidth = 5;
            const int nameColumnWidth = 25;
            const int macColumnWidth = 25;
            const int rssiColumnWidth = 15;
            const int IsConnectedColumnWidth = 15;

            ThreadSafeConsole.WriteLine($"\ntei-Pens in Reichweite: {discoveredPens.Count}");
            ThreadSafeConsole.WriteLine($"{"No.".PadRight(noColumnWidth)}{"Name".PadRight(nameColumnWidth)}{"MAC-Adresse".PadRight(macColumnWidth)}{"RSSI dBm".PadRight(rssiColumnWidth)}{"Verbunden".PadRight(IsConnectedColumnWidth)}");
            
            int index = 1;
            foreach (var pen in discoveredPens)
            {
                string noColumn = $"{index}.".PadRight(noColumnWidth);
                string nameColumn = (pen.Name ?? "").PadRight(nameColumnWidth);
                
                // MAC-Adresse als primärer Identifier verwenden
                string macAddress = pen.MacAddress ?? "";
                string macColumn = macAddress.PadRight(macColumnWidth);
                string rssiColumn = $"{pen.Rssi} dBm".PadRight(rssiColumnWidth);
                
                // Prüfen, ob der Stift bereits verbunden ist (nur mit MAC-Adresse)
                bool isConnected = !string.IsNullOrEmpty(macAddress) && (isConnectedCallback?.Invoke(macAddress) ?? false);

                string isConnectedColumn = (isConnected ? "Ja" : "Nein").PadRight(IsConnectedColumnWidth);
                ThreadSafeConsole.WriteLine($"{noColumn}{nameColumn}{macColumn}{rssiColumn}{isConnectedColumn}");
                index++;
            }
        }

        /// <summary>
        /// Zeigt die Verbindungsinformationen des verbundenen tei-Pens an.
        /// </summary>
        /// <param name="connectedPen">Verbindungsinformationen des verbundenen tei-Pens.</param>
        public static void DisplayConnectedPens(PenConnectionInfoModel? connectedPen)
        {
            if (connectedPen == null)
            {
                ThreadSafeConsole.WriteLine("Keine verbundenen tei-Pens verfügbar.");
                return;
            }

            
            const int nameColumnWidth = 15;
            const int macColumnWidth = 25;
            const int rssiColumnWidth = 15;
            const int statusColumnWidth = 25;
            const int batteryColumnWidth = 15;
           

            // MAC-Adresse als primärer Identifier verwenden
            string macAddress = connectedPen.MacAddress ?? "";
            
            // Status-Text basierend auf ConnectionState
            string statusText = connectedPen.ConnectionState switch
            {
                PenConnectionState.Disconnected => "Getrennt",
                PenConnectionState.Connected => "Verbunden (Warte auf Authentifizierung)",
                PenConnectionState.PasswordRequired => "Verbunden (Passwort benötigt)",
                PenConnectionState.Authenticated => "Verbunden (authentifiziert)",
                _ => "Unbekannt"
            };

            // Batteriestatus-Text
            string batteryText = connectedPen.BatteryStatus.HasValue 
                ? $"{connectedPen.BatteryStatus.Value}%" 
                : "N/A";
            
            ThreadSafeConsole.WriteLine($"\nVerbundene tei-Pens: {macAddress}");
            ThreadSafeConsole.WriteLine($"{"Name".PadRight(nameColumnWidth)}{"MAC-Adresse".PadRight(macColumnWidth)}{"RSSI dBm".PadRight(rssiColumnWidth)}{"Status".PadRight(statusColumnWidth)}{"Batterie".PadRight(batteryColumnWidth)}");
            
            string nameColumn = (connectedPen.DisplayName ?? "").PadRight(nameColumnWidth);
            string macColumn = macAddress.PadRight(macColumnWidth);
            string rssiColumn = $"{connectedPen.Rssi} dBm".PadRight(rssiColumnWidth);
            string statusColumn = statusText.PadRight(statusColumnWidth);
            string batteryColumn = batteryText.PadRight(batteryColumnWidth);

            ThreadSafeConsole.WriteLine($"{nameColumn}{macColumn}{rssiColumn}{statusColumn}{batteryColumn}");
            

        }

        /// <summary>
        /// Zeigt alle bereits gekoppelten tei-Pens in der memory.json an.
        /// </summary>
        /// <param name="pairedPens">Gekoppelte tei-Pens.</param>
        public static void DisplayPairedPens(Dictionary<string, PenMemoryEntry> pairedPens)
        {
            if (pairedPens == null || pairedPens.Count == 0)
            {
                ThreadSafeConsole.WriteLine("Keine gekoppelten tei-Pens verfügbar.");
                return;
            }

            const int noColumnWidth = 5;
            const int nameColumnWidth = 20;
            const int modelColumnWidth = 25;
            const int macColumnWidth = 25;
            const int firstConnectedColumnWidth = 20;
            const int lastConnectedColumnWidth = 20;

            ThreadSafeConsole.WriteLine($"\nGekoppelte tei-Pens: {pairedPens.Count}");
            ThreadSafeConsole.WriteLine($"{"No.".PadRight(noColumnWidth)}{"Name".PadRight(nameColumnWidth)}{"Modell".PadRight(modelColumnWidth)}{"MAC-Adresse".PadRight(macColumnWidth)}{"Erst verbunden".PadRight(firstConnectedColumnWidth)}{"Zuletzt verbunden".PadRight(lastConnectedColumnWidth)}");
            
            int index = 1;
            foreach (var kvp in pairedPens)
            {
                var pen = kvp.Value;
                string noColumn = $"{index}.".PadRight(noColumnWidth);
                // DisplayName bevorzugen, falls vorhanden, sonst PenName
                string displayName = !string.IsNullOrEmpty(pen.DisplayName) ? pen.DisplayName : pen.PenName;
                string nameColumn = (displayName ?? "").PadRight(nameColumnWidth);
                string modelColumn = (pen.PenName ?? "").PadRight(modelColumnWidth);
                string macColumn = (pen.MacAddress ?? "").PadRight(macColumnWidth);
                string firstConnectedColumn = pen.FirstConnectedAt.ToString("yyyy-MM-dd HH:mm").PadRight(firstConnectedColumnWidth);
                string lastConnectedColumn = pen.LastConnectedAt.ToString("yyyy-MM-dd HH:mm").PadRight(lastConnectedColumnWidth);
                
                ThreadSafeConsole.WriteLine($"{noColumn}{nameColumn}{modelColumn}{macColumn}{firstConnectedColumn}{lastConnectedColumn}");
                index++;
            }
        }
    }
}

