using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace tei_penService_ui
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        private const int SwShow = 5;
        private const string StartupAuditFileName = "TEI_penService_startup.txt";

        /// <summary>Elternprozess (Konsole von cmd/PowerShell/Terminal, aus dem die App gestartet wurde).</summary>
        private const uint AttachParentProcess = 0xFFFFFFFF;

        public App()
        {
            WriteStartupAudit("App ctor (vor InitializeComponent in Main)");
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetConsoleOutputCP(uint wCodePageId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Konsole vor <see cref="Application.OnStartup"/> einrichten, damit <see cref="Console"/> nicht mit ungültigen Handles cached.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            WriteStartupAudit("OnStartup begin");

            try
            {
                bool openedOwnConsole = false;
                bool consoleReady = false;

                if (AllocConsole())
                {
                    consoleReady = true;
                    openedOwnConsole = true;
                }
                else if (AttachConsole(AttachParentProcess))
                {
                    consoleReady = true;
                }

                if (!consoleReady)
                {
                    int err = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"Konsole: keine Zuordnung (Attach/Alloc), Win32={err}");
                    string auditHint = Path.Combine(Path.GetTempPath(), StartupAuditFileName);
                    Debug.WriteLine("Hinweis: Konsole nicht zuordenbar. Diagnose: " + auditHint);
                }
                else
                {
                    SetConsoleOutputCP(65001);
                    var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                    try
                    {
                        RebindDotNetStandardStreams(utf8);
                        Console.OutputEncoding = utf8;
                        Console.InputEncoding = utf8;
                    }
                    catch (Exception rex)
                    {
                        WriteStartupAudit("RebindDotNetStandardStreams: " + rex.Message);
                    }

                    string banner =
                        (openedOwnConsole
                            ? "Konsole: separates Fenster (z. B. Start per Doppelklick).\r\n"
                            : "Konsole: Ausgabe in diesem Terminal (Start aus cmd/PowerShell).\r\n") +
                        "\r\n";
                    Console.Write(banner);
                    WriteStartupAudit("Konsole: Banner geschrieben");

                    if (openedOwnConsole)
                    {
                        IntPtr hwnd = GetConsoleWindow();
                        if (hwnd != IntPtr.Zero)
                        {
                            ShowWindow(hwnd, SwShow);
                            SetForegroundWindow(hwnd);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteStartupAudit("OnStartup exception: " + ex);
                Debug.WriteLine($"Konsole-Init: {ex.Message}");
            }

            WriteStartupAudit("OnStartup vor base");
            base.OnStartup(e);
        }

        private static void WriteStartupAudit(string message)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}\r\n";
                string path = Path.Combine(Path.GetTempPath(), StartupAuditFileName);
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Nach <c>AllocConsole</c>/<c>AttachConsole</c> Stdout/Stderr neu an die Konsole binden (sonst leere Ausgabe).
        /// </summary>
        private static void RebindDotNetStandardStreams(UTF8Encoding utf8)
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8, bufferSize: 256) { AutoFlush = true };
            Console.SetOut(stdout);
            var stderr = new StreamWriter(Console.OpenStandardError(), utf8, bufferSize: 256) { AutoFlush = true };
            Console.SetError(stderr);
        }
    }
}
