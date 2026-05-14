using System;
using System.Threading.Tasks;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Utilities;

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Verarbeitet Benutzerbefehle und führt entsprechende Aktionen aus.
    /// </summary>
    public class CommandProcessor
    {
        private readonly TeiPenService _teiPenService;

        /// <summary>
        /// Initialisiert den CommandProcessor.
        /// </summary>
        /// <param name="teiPenService">Der TeiPenService für die Ausführung der Befehle.</param>
        public CommandProcessor(TeiPenService teiPenService)
        {
            _teiPenService = teiPenService;
        }

        /// <summary>
        /// Zeigt die Verbindungsoptionen automatisch an, wenn eine Verbindung hergestellt wurde.
        /// </summary>
        private void DisplayConnectionOptionsIfConnected()
        {
            var connectedPen = _teiPenService.GetConnectedPenInfo();
            if (connectedPen != null && connectedPen.ConnectionState == PenConnectionState.Authenticated)
            {
                ConsoleInterfaceService.DisplayConnectionOptions(connectedPen.ConnectionState);
            }
        }

        /// <summary>
        /// Verarbeitet einen Benutzerbefehl und führt die entsprechende Aktion aus.
        /// </summary>
        /// <param name="command">Der Benutzerbefehl (original, Groß-/Kleinschreibung wird beibehalten).</param>
        /// <returns>True, wenn die Anwendung beendet werden soll, false sonst.</returns>
        public async Task<bool> ProcessCommand(string command)
        {
            // Prüfen, ob auf Passwort-Eingabe gewartet wird
            // Wenn ja, wird die Eingabe direkt als Passwort interpretiert
            if (_teiPenService.IsPasswordRequired())
            {
                bool success = _teiPenService.InputPassword(command.Trim());
                if (!success)
                {
                    ThreadSafeConsole.WriteLine("Bitte versuchen Sie es erneut.");
                }
                return false;
            }

            // Befehl in Kleinbuchstaben für case-insensitive Vergleich, aber Parameter original behalten
            string commandLower = command.ToLowerInvariant();
            string firstPart = commandLower.Split(new[] { ' ' }, 2)[0];
            
            switch (firstPart)
            {
                case "s":
                    // Gerätesuche starten
                    _teiPenService.StartDeviceDiscovery();
                    break;
                case "b":
                    // Gerätesuche beenden
                    _teiPenService.StopDeviceDiscovery();
                    break;
                case "bt":
                    // Bluetooth aktivieren/deaktivieren
                    try
                    {
                        bool currentStatus = await _teiPenService.IsBluetoothEnabledAsync();
                        bool success = await _teiPenService.SetBluetoothEnabledAsync(!currentStatus);
                        if (success)
                        {
                            ThreadSafeConsole.WriteLine($"Bluetooth wurde {(!currentStatus ? "aktiviert" : "deaktiviert")}.");
                        }
                        else
                        {
                            ThreadSafeConsole.WriteLine($"Fehler beim {(currentStatus ? "Deaktivieren" : "Aktivieren")} von Bluetooth.");
                        }
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Fehler beim Umschalten von Bluetooth: {ex.Message}");
                    }
                    break;
                case "q":
                    // Bluetoothverwaltungsinterface beenden
                    _teiPenService.Dispose();
                    return true;
                case "a":
                    // Aktionen anzeigen
                    var connectedPen = _teiPenService.GetConnectedPenInfo();
                    var connectionState = connectedPen?.ConnectionState ?? PenConnectionState.Disconnected;
                    ConsoleInterfaceService.DisplayMenu(connectionState);
                    break;
                case "p":
                    // Stifte in Reichweite anzeigen
                    var discoveredPens = _teiPenService.GetDiscoveredPens();
                    ConsoleInterfaceService.DisplayDiscoveredPens(discoveredPens, (macAddress) => _teiPenService.IsPenConnected(macAddress));
                    break;
                case "l":
                    // Verbundenen tei-Pen anzeigen
                    var connectedPenInfo = _teiPenService.GetConnectedPenInfo();
                    ConsoleInterfaceService.DisplayConnectedPens(connectedPenInfo);
                    break;
                case "bat":
                case "battery":
                    // Batteriestatus abfragen
                    var connectedPenForBattery = _teiPenService.GetConnectedPenInfo();
                    if (connectedPenForBattery == null || connectedPenForBattery.ConnectionState != PenConnectionState.Authenticated)
                    {
                        ThreadSafeConsole.WriteLine("Kein Stift verbunden. Bitte zuerst eine Verbindung herstellen.");
                    }
                    else
                    {
                        _teiPenService.RequestBatteryStatus();
                        ThreadSafeConsole.WriteLine("Batteriestatus wird angefordert...");
                        // Kurze Verzögerung, damit das Event verarbeitet werden kann
                        await Task.Delay(500);
                        var updatedPenInfo = _teiPenService.GetConnectedPenInfo();
                        if (updatedPenInfo?.BatteryStatus.HasValue == true)
                        {
                            ThreadSafeConsole.WriteLine($"Batteriestatus: {updatedPenInfo.BatteryStatus.Value}%");
                        }
                        else
                        {
                            ThreadSafeConsole.WriteLine("Batteriestatus konnte nicht abgerufen werden. Bitte versuchen Sie es erneut.");
                        }
                    }
                    break;
                case "d":
                    // Trenne die aktive Verbindung zum tei-Pen
                    await _teiPenService.DisconnectFromPenAsync();
                    // Keine automatische Anzeige bei Trennung (laut Anforderung)
                    break;
                case "k":
                    // Gekoppelte tei-Pens anzeigen
                    var pairedPens = await _teiPenService.GetPairedPensAsync();
                    ConsoleInterfaceService.DisplayPairedPens(pairedPens);
                    break;
                case "r":
                    // Alle gekoppelten tei-Pens entfernen
                    await _teiPenService.RemoveAllPairedPensAsync();
                    break;
                default:
                    // Parametrisierte Befehle behandeln (case-insensitive für Befehl, aber Parameter original)
                    if (commandLower.StartsWith("c ") && command.Length > 2)
                    {
                        string macAddress = command.Substring(2).Trim();
                        bool connected = await _teiPenService.ConnectToPenAsync(macAddress);
                        // Verbindungsoptionen werden automatisch über ConnectionStatusChanged Event angezeigt
                        // Keine manuelle Anzeige hier nötig
                    }
                    else if (firstPart == "r" && commandLower.StartsWith("r ") && command.Length > 2)
                    {
                        // Nur wenn "r" allein steht, nicht wenn es "removepassword" ist
                        string macAddress = command.Substring(2).Trim();
                        await _teiPenService.RemovePairedPenAsync(macAddress);
                    }
                    else if (commandLower.StartsWith("setname ") && command.Length > 8)
                    {
                        // DisplayName original behalten (Groß-/Kleinschreibung)
                        string displayName = command.Substring(8).Trim();
                        if (string.IsNullOrEmpty(displayName))
                        {
                            ThreadSafeConsole.WriteLine("Bitte geben Sie einen DisplayName an: setname <name>");
                        }
                        else
                        {
                            await _teiPenService.SetDisplayNameAsync(displayName);
                        }
                    }
                    else if (commandLower.StartsWith("setpassword "))
                    {
                        // Passwörter original behalten
                        string[] parts = command.Substring(12).Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 2)
                        {
                            ThreadSafeConsole.WriteLine("Bitte geben Sie altes und neues Passwort an: setpassword <old> <new>");
                            ThreadSafeConsole.WriteLine("Hinweis: Verwenden Sie \"\" (leeres String) als <old>, wenn noch kein Passwort gesetzt wurde.");
                        }
                        else
                        {
                            // Leeres String für oldPassword erlauben (bedeutet: noch kein Passwort gesetzt)
                            string oldPassword = parts[0];
                            await _teiPenService.SetPasswordAsync(oldPassword, parts[1]);
                        }
                    }
                    else if (commandLower.StartsWith("removepassword ") && command.Length > 15)
                    {
                        // Passwort original behalten
                        string currentPassword = command.Substring(15).Trim();
                        if (string.IsNullOrEmpty(currentPassword))
                        {
                            ThreadSafeConsole.WriteLine("Bitte geben Sie das aktuelle Passwort an: removepassword <current>");
                        }
                        else
                        {
                            await _teiPenService.RemovePasswordAsync(currentPassword);
                        }
                    }
                    else if (commandLower == "deleteallnotes")
                    {
                        // Alle Notizen des verbundenen Stifts löschen
                        var connectedPenForDelete = _teiPenService.GetConnectedPenInfo();
                        if (connectedPenForDelete == null || connectedPenForDelete.ConnectionState != PenConnectionState.Authenticated)
                        {
                            ThreadSafeConsole.WriteLine("Kein Stift verbunden. Bitte zuerst eine Verbindung herstellen.");
                        }
                        else
                        {
                            ThreadSafeConsole.WriteLine("WARNUNG: Alle Notizen des Stifts werden gelöscht. Dieser Vorgang kann nicht rückgängig gemacht werden.");
                            _teiPenService.DeleteAllNotes();
                        }
                    }
                    else
                    {
                        // Falsche Eingabe
                        ThreadSafeConsole.WriteLine("Falsche Eingabe. Bitte wählen Sie eine gültige Aktion.");
                    }
                    break;
            }

            return false;
        }
    }
}

