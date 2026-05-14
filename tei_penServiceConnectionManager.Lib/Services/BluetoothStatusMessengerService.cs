using System;
using System.Threading.Tasks;
using TeiPenServiceConnectionManager.Utilities;

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Verwaltet die Bluetooth-Status-Änderungsbenachrichtigungen, sowie die while Aufforderung, die den Benutzer auf die Notwendigkeit der Bluetooth-Aktivierung hinweist.
    /// </summary>
    public class BluetoothStatusMessengerService
    {
        /// <summary>
        /// Handler für das BluetoothStatusChanged Event.
        /// Wird aufgerufen, wenn der Bluetooth-Status sich während der Laufzeit ändert.
        /// </summary>
        public static void HandleBluetoothStatusChanged(bool isEnabled)
        {
            if (!isEnabled)
            {
                ThreadSafeConsole.WriteLine("\n⚠️  WARNUNG: Bluetooth wurde auf diesem Gerät deaktiviert!");
                ThreadSafeConsole.WriteLine("Bitte aktivieren Sie Bluetooth wieder, um die Funktionalität zu nutzen.");
                ThreadSafeConsole.WriteLine("Die Gerätesuche wurde automatisch gestoppt.");
            }
            else
            {
                ThreadSafeConsole.WriteLine("\nBluetooth wurde wieder aktiviert.");
            }
        }

        /// <summary>
        /// Wartet, bis Bluetooth auf dem Host-Gerät aktiviert ist.
        /// Wenn Bluetooth nicht aktiviert ist, wird eine Warnung angezeigt und der Benutzer aufgefordert, Bluetooth zu aktivieren.
        /// Prüft alle 2 Sekunden, ob Bluetooth aktiviert ist.
        /// </summary>
        public static async Task WaitForBluetoothEnabledAsync(TeiPenService teiPenService)
        {
            const int checkIntervalMs = 2000; // 2 Sekunden
            
            // initiale Prüfung, ob Bluetooth aktiviert ist
            bool isBluetoothEnabled = await teiPenService.IsBluetoothEnabledAsync();

            if (isBluetoothEnabled)
            {
                ThreadSafeConsole.WriteLine("Bluetooth ist aktiviert. Du kannst nun mit der Verwaltung deines tei-Pens beginnen.");
                return;
            }

            // Bluetooth nicht aktiviert, Warteschleife starten
            ThreadSafeConsole.WriteLine("Bluetooth ist nicht aktiviert. Bitte aktiviere Bluetooth, um fortzufahren.");


            int dotCount = 0;
            while (!isBluetoothEnabled)
            {
                // Warte 2 Sekunden
                await Task.Delay(checkIntervalMs);

                // Prüfe erneut, ob Bluetooth aktiviert ist
                isBluetoothEnabled = await teiPenService.IsBluetoothEnabledAsync();

                // Fortschrittsanzei (animierte Punkte)
                dotCount = (dotCount + 1) % 4;
                string dots = new string('.', dotCount);
                ThreadSafeConsole.Write($"\rWarte auf Bluetooth-Aktivierung{dots}  ");
            }

            // Bluetooth wurde aktiviert
            ThreadSafeConsole.WriteLine("\nBluetooth wurde aktiviert. Du kannst nun mit der Verwaltung deines tei-Pens beginnen.");
        }
    }
}

