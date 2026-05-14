using System;
using System.Threading.Tasks;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Utilities;

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Orchestriert den gesamten Anwendungsablauf.
    /// </summary>
    public class ApplicationControllerService
    {
        /// <summary>
        /// Startet die Anwendung und verwaltet den Haupt-Lebenszyklus.
        /// </summary>
        public static async Task RunAsync()
        {
            ThreadSafeConsole.WriteLine("Willkommen in deinem tei-Pen-Bluetoothverwaltungsinterface.");
            
            using (var teiPenService = new TeiPenService())
            {
                
                // Initiale Bluetooth-Status-Prüfung mit Warteschleife
                await BluetoothStatusMessengerService.WaitForBluetoothEnabledAsync(teiPenService);

                

                // Bluetooth-Status-Überwachung abonnieren
                teiPenService.BluetoothStatusChanged += (sender, isEnabled) => BluetoothStatusMessengerService.HandleBluetoothStatusChanged(isEnabled);
                
                // Verbindungsstatus-Änderungen abonnieren, um Optionen automatisch anzuzeigen
                teiPenService.ConnectionStatusChanged += (sender, isConnected) =>
                {
                    // Nur bei vollständiger Authentifizierung die 3 neuen Optionen anzeigen, bei Trennung nichts
                    if (isConnected)
                    {
                        var connectedPen = teiPenService.GetConnectedPenInfo();
                        if (connectedPen != null && connectedPen.ConnectionState == PenConnectionState.Authenticated)
                        {
                            ConsoleInterfaceService.DisplayConnectionOptions(connectedPen.ConnectionState);
                        }
                    }
                };
                
                string userInput;
                
                // Inputoptionen anzeigen
                var connectedPenInitial = teiPenService.GetConnectedPenInfo();
                var connectionStateInitial = connectedPenInitial?.ConnectionState ?? PenConnectionState.Disconnected;
                ConsoleInterfaceService.DisplayMenu(connectionStateInitial);
                
                var commandProcessor = new CommandProcessor(teiPenService);
                
                do
                {
                   
                    
                    // User Input lesen (original, ohne Konvertierung), leere Eingaben werden als "" behandelt
                    userInput = ThreadSafeConsole.ReadLine()?.Trim() ?? "";

                    bool shouldQuit = await commandProcessor.ProcessCommand(userInput);

                    if (shouldQuit)
                    {
                        break;
                    }

                } while (true);
            }
        }
    }
}

