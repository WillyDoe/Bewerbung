using System;
using System.Collections.Generic;

#nullable enable

namespace TeiPenServiceConnectionManager.Utilities
{
    /// <summary>
    /// Thread-sichere Diagnose-Ausgabe in die gebundene <see cref="Console"/> (schwarzes Fenster nach <c>AllocConsole</c>
    /// bzw. Start-Terminal), nicht in die Debugger-„Debug Console“.
    /// Synchronisiert Zugriffe; puffert während einer aktiven Konsolen-Benutzereingabe (ReadLine).
    /// </summary>
    public static class ThreadSafeConsole
    {
        private static readonly object _consoleLock = new object();
        private static readonly Queue<string> _outputBuffer = new Queue<string>();
        private static bool _isReadingInput = false;

        /// <summary>
        /// Schreibt die angegebene Zeichenfolge, gefolgt vom Zeilenabschlusszeichen, in die Standardausgabe.
        /// Wenn gerade eine Benutzereingabe läuft, wird die Ausgabe gepuffert.
        /// </summary>
        /// <param name="value">Die zu schreibende Zeichenfolge. Wenn null, wird nur der Zeilenabschluss geschrieben.</param>
        
        public static void WriteLine(string? value = null)
        {
            lock (_consoleLock)
            {
                if (_isReadingInput)
                {
                    // Während Eingabe: Ausgabe puffern
                    _outputBuffer.Enqueue(value ?? string.Empty);
                }
                else
                {
                    TryConsoleWriteLine(value);
                }
            }
        }

        /// <summary>
        /// Schreibt die angegebene Zeichenfolge in die Standardausgabe.
        /// Wenn gerade eine Benutzereingabe läuft, wird die Ausgabe gepuffert.
        /// </summary>
        /// <param name="value">Die zu schreibende Zeichenfolge. Wenn null, wird nichts geschrieben.</param>
        public static void Write(string? value = null)
        {
            lock (_consoleLock)
            {
                if (_isReadingInput)
                {
                    // Während Eingabe: Ausgabe puffern
                    if (value != null)
                    {
                        _outputBuffer.Enqueue(value);
                    }
                }
                else
                {
                    TryConsoleWrite(value);
                }
            }
        }

        private static void TryConsoleWriteLine(string? value)
        {
            AttachedConsoleWriter.WriteLine(value);
        }

        private static void TryConsoleWrite(string? value)
        {
            AttachedConsoleWriter.Write(value);
        }

        /// <summary>
        /// Liest die nächste Zeile von Zeichen aus der Standardeingabe.
        /// Während der Eingabe werden alle Ausgaben gepuffert und nach der Eingabe angezeigt.
        /// Das Puffern beginnt erst, wenn der Benutzer tatsächlich Zeichen eingibt.
        /// </summary>
        /// <returns>Die nächste Zeile von Zeichen aus der Standardeingabe. Wenn keine Zeile vorhanden ist, wird null zurückgegeben.</returns>
        public static string? ReadLine()
        {
            System.Text.StringBuilder input = new System.Text.StringBuilder();
            bool hasInputStarted = false;

            try
            {
                // Zeichen einzeln lesen, bis Enter gedrückt wird
                while (true)
                {
                    // WICHTIG: Prüfen, ob Console.KeyAvailable ist, um Blockierungen zu vermeiden
                    // Aber ReadKey blockiert immer, also müssen wir es trotzdem aufrufen
                    ConsoleKeyInfo keyInfo = Console.ReadKey(true); // true = nicht anzeigen, wir machen das selbst

                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        // Enter gedrückt - Eingabe beenden
                        lock (_consoleLock)
                        {
                            _isReadingInput = false;

                            AttachedConsoleWriter.WriteLine();
                            while (_outputBuffer.Count > 0)
                                AttachedConsoleWriter.WriteLine(_outputBuffer.Dequeue());
                        }
                        
                        return input.Length > 0 ? input.ToString() : string.Empty;
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        // Backspace - letztes Zeichen entfernen
                        if (input.Length > 0)
                        {
                            input.Length--;
                            Console.Write("\b \b"); // Cursor zurück, Leerzeichen, Cursor wieder zurück
                        }
                    }
                    else if (!char.IsControl(keyInfo.KeyChar))
                    {
                        // Normales Zeichen - hinzufügen
                        if (!hasInputStarted)
                        {
                            // Erstes Zeichen wurde eingegeben - jetzt beginnen wir mit dem Puffern
                            lock (_consoleLock)
                            {
                                _isReadingInput = true;
                            }
                            hasInputStarted = true;
                        }
                        
                        input.Append(keyInfo.KeyChar);
                        Console.Write(keyInfo.KeyChar); // Zeichen anzeigen
                    }
                    // Andere Steuerzeichen ignorieren
                }
            }
            catch
            {
                lock (_consoleLock)
                {
                    _isReadingInput = false;
                    // Bei Fehler: Puffer leeren
                    _outputBuffer.Clear();
                }
                // Exception weiterwerfen, damit der Aufrufer sie behandeln kann
                throw;
            }
        }
    }
}