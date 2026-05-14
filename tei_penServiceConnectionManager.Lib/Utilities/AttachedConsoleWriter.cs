using System;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

namespace TeiPenServiceConnectionManager.Utilities
{
    /// <summary>
    /// Schreibt primär per <c>WriteConsole</c> (echtes Konsolenfenster); Fallback <see cref="Console.Write"/>.
    /// Ein gemeinsamer Lock serialisiert Lib- und OCR-Ausgabe.
    /// </summary>
    public static class AttachedConsoleWriter
    {
        private const int StdOutputHandle = -11;
        private static readonly object Sync = new object();
        private static readonly object ConOutInitLock = new object();
        private static IntPtr _reservedConOut = IntPtr.Zero;

        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareRead = 1;
        private const uint FileShareWrite = 2;
        private const uint OpenExisting = 3;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool WriteConsole(IntPtr hConsoleOutput, string lpBuffer, int nNumberOfCharsToWrite, out int lpNumberOfCharsWritten, IntPtr lpReserved);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
        private static extern IntPtr CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(
            IntPtr hFile,
            byte[] lpBuffer,
            int nNumberOfBytesToWrite,
            out int lpNumberOfBytesWritten,
            IntPtr lpOverlapped);

        public static void Write(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            lock (Sync)
            {
                if (TryWriteViaKernel32(text!))
                    return;

                try
                {
                    Console.Write(text);
                    Console.Out.Flush();
                }
                catch
                {
                }
            }
        }

        public static void WriteLine(string? text = null)
        {
            Write(text == null ? Environment.NewLine : text + Environment.NewLine);
        }

        private static bool TryWriteViaKernel32(string text)
        {
            try
            {
                IntPtr h = ResolveConsoleOutputHandle();
                if (h == IntPtr.Zero || h == InvalidHandleValue)
                    return false;
                if (WriteConsole(h, text, text.Length, out _, IntPtr.Zero))
                    return true;
                return TryWriteFileUtf8(h, text);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryWriteFileUtf8(IntPtr h, string text)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                if (bytes.Length == 0)
                    return true;
                return WriteFile(h, bytes, bytes.Length, out int written, IntPtr.Zero) && written == bytes.Length;
            }
            catch
            {
                return false;
            }
        }

        private static IntPtr ResolveConsoleOutputHandle()
        {
            IntPtr h = GetStdHandle(StdOutputHandle);
            if (h != IntPtr.Zero && h != InvalidHandleValue)
                return h;

            lock (ConOutInitLock)
            {
                if (_reservedConOut != IntPtr.Zero && _reservedConOut != InvalidHandleValue)
                    return _reservedConOut;

                IntPtr c = CreateFileW(
                    "CONOUT$",
                    GenericRead | GenericWrite,
                    FileShareRead | FileShareWrite,
                    IntPtr.Zero,
                    OpenExisting,
                    0,
                    IntPtr.Zero);
                if (c != InvalidHandleValue)
                    _reservedConOut = c;
                return _reservedConOut;
            }
        }
    }
}
